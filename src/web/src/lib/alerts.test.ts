import { describe, expect, it, vi } from 'vitest'
import { createBrowserAlerts } from './alerts'

/** Dublê mínimo da Web Audio API — jsdom não implementa AudioContext. */
function fakeAudio() {
  const oscillator = { type: '', frequency: { value: 0 }, connect: vi.fn(), start: vi.fn(), stop: vi.fn() }
  const gain = { gain: { value: 0 }, connect: vi.fn() }
  const context = {
    currentTime: 0,
    state: 'running' as AudioContextState,
    destination: {},
    resume: vi.fn(),
    createOscillator: vi.fn(() => oscillator),
    createGain: vi.fn(() => gain),
  }

  return { context, oscillator, gain, Ctor: vi.fn(() => context) as unknown as new () => AudioContext }
}

function fakeNotification(permission: NotificationPermission) {
  const constructed: { title: string; options?: NotificationOptions }[] = []
  const requestPermission = vi.fn(async () => 'granted' as NotificationPermission)

  class FakeNotification {
    static permission = permission
    static requestPermission = requestPermission
    constructor(title: string, options?: NotificationOptions) {
      constructed.push({ title, options })
    }
  }

  return { Ctor: FakeNotification as unknown as typeof Notification, constructed, requestPermission }
}

describe('prepare', () => {
  it('deve pedir permissão de notificação quando ainda não foi decidida', () => {
    const notification = fakeNotification('default')
    const alerts = createBrowserAlerts({ notification: notification.Ctor })

    alerts.prepare()

    expect(notification.requestPermission).toHaveBeenCalledTimes(1)
  })

  it('não deve pedir permissão de novo depois de concedida', () => {
    const notification = fakeNotification('granted')
    const alerts = createBrowserAlerts({ notification: notification.Ctor })

    alerts.prepare()

    expect(notification.requestPermission).not.toHaveBeenCalled()
  })

  it('não deve insistir depois de negada', () => {
    const notification = fakeNotification('denied')
    const alerts = createBrowserAlerts({ notification: notification.Ctor })

    alerts.prepare()

    expect(notification.requestPermission).not.toHaveBeenCalled()
  })

  it('deve destravar o áudio no gesto do usuário', () => {
    // Navegadores só liberam áudio a partir de uma interação. O ciclo termina
    // 25 minutos depois, longe de qualquer gesto: destravar no clique é o que
    // faz o alarme realmente tocar lá na frente.
    const audio = fakeAudio()
    const alerts = createBrowserAlerts({ audioContext: audio.Ctor })

    alerts.prepare()

    expect(audio.Ctor).toHaveBeenCalledTimes(1)
  })

  it('deve reaproveitar o mesmo contexto de áudio entre ciclos', () => {
    const audio = fakeAudio()
    const alerts = createBrowserAlerts({ audioContext: audio.Ctor })

    alerts.prepare()
    alerts.prepare()

    expect(audio.Ctor).toHaveBeenCalledTimes(1)
  })

  it('deve funcionar em navegador sem Web Audio nem Notification', () => {
    const alerts = createBrowserAlerts({ audioContext: undefined, notification: undefined })

    expect(() => alerts.prepare()).not.toThrow()
  })
})

describe('playAlarm', () => {
  it('deve emitir um tom audível', () => {
    const audio = fakeAudio()
    const alerts = createBrowserAlerts({ audioContext: audio.Ctor })
    alerts.prepare()

    alerts.playAlarm()

    expect(audio.oscillator.start).toHaveBeenCalled()
    expect(audio.oscillator.stop).toHaveBeenCalled()
    expect(audio.gain.gain.value).toBeGreaterThan(0)
  })

  it('deve retomar um contexto suspenso pelo navegador', () => {
    const audio = fakeAudio()
    audio.context.state = 'suspended'
    const alerts = createBrowserAlerts({ audioContext: audio.Ctor })
    alerts.prepare()

    alerts.playAlarm()

    expect(audio.context.resume).toHaveBeenCalled()
  })

  it('não deve quebrar sem Web Audio disponível', () => {
    const alerts = createBrowserAlerts({ audioContext: undefined })

    expect(() => alerts.playAlarm()).not.toThrow()
  })

  it('não deve quebrar quando o áudio nunca foi destravado', () => {
    const audio = fakeAudio()
    const alerts = createBrowserAlerts({ audioContext: audio.Ctor })

    expect(() => alerts.playAlarm()).not.toThrow()
  })
})

describe('notify', () => {
  it('deve criar a notificação quando há permissão', () => {
    const notification = fakeNotification('granted')
    const alerts = createBrowserAlerts({ notification: notification.Ctor })

    alerts.notify({ title: 'Pomodoro concluído', body: 'Hora da pausa.' })

    expect(notification.constructed).toHaveLength(1)
    expect(notification.constructed[0]?.title).toBe('Pomodoro concluído')
    expect(notification.constructed[0]?.options?.body).toBe('Hora da pausa.')
  })

  it('não deve notificar sem permissão concedida', () => {
    const notification = fakeNotification('default')
    const alerts = createBrowserAlerts({ notification: notification.Ctor })

    alerts.notify({ title: 'Pomodoro concluído', body: 'Hora da pausa.' })

    expect(notification.constructed).toHaveLength(0)
  })

  it('não deve quebrar em navegador sem a API', () => {
    const alerts = createBrowserAlerts({ notification: undefined })

    expect(() => alerts.notify({ title: 'x', body: 'y' })).not.toThrow()
  })
})

describe('isHidden', () => {
  it('deve refletir a visibilidade do documento', () => {
    const alerts = createBrowserAlerts({ visibility: { hidden: true } })

    expect(alerts.isHidden()).toBe(true)
  })
})
