/**
 * Porto de alertas: som e notificação do navegador.
 *
 * Todas as APIs entram por injeção para que o comportamento seja testável sem
 * navegador — jsdom não implementa Web Audio nem Notification.
 */

export interface AlertMessage {
  readonly title: string
  readonly body: string
}

export interface AlertPorts {
  /**
   * Chamado a partir de um gesto do usuário. Destrava o áudio e pede permissão
   * de notificação; sem isso o alarme não toca 25 minutos depois, quando não há
   * mais gesto nenhum por perto.
   */
  prepare(): void
  playAlarm(): void
  notify(message: AlertMessage): void
  isHidden(): boolean
}

export interface BrowserAlertsDeps {
  readonly audioContext?: (new () => AudioContext) | undefined
  readonly notification?: typeof Notification | undefined
  readonly visibility?: Pick<Document, 'hidden'> | undefined
}

const ALARM_FREQUENCY_HZ = 880
const ALARM_DURATION_S = 0.4
const ALARM_VOLUME = 0.15

export function createBrowserAlerts(deps: BrowserAlertsDeps = {}): AlertPorts {
  const AudioContextCtor = deps.audioContext ?? globalThis.AudioContext
  const NotificationCtor = deps.notification ?? globalThis.Notification
  const visibility = deps.visibility ?? globalThis.document

  let context: AudioContext | undefined

  return {
    prepare() {
      if (AudioContextCtor && !context) context = new AudioContextCtor()
      if (NotificationCtor && NotificationCtor.permission === 'default') {
        void NotificationCtor.requestPermission()
      }
    },

    playAlarm() {
      if (!context) return

      // O navegador suspende o contexto quando a aba fica muito tempo em
      // segundo plano; sem retomar, o oscilador toca no silêncio.
      if (context.state === 'suspended') void context.resume()

      const oscillator = context.createOscillator()
      const gain = context.createGain()

      oscillator.type = 'sine'
      oscillator.frequency.value = ALARM_FREQUENCY_HZ
      gain.gain.value = ALARM_VOLUME

      oscillator.connect(gain)
      gain.connect(context.destination)
      oscillator.start()
      oscillator.stop(context.currentTime + ALARM_DURATION_S)
    },

    notify(message) {
      if (!NotificationCtor || NotificationCtor.permission !== 'granted') return

      new NotificationCtor(message.title, { body: message.body })
    },

    isHidden: () => visibility.hidden,
  }
}
