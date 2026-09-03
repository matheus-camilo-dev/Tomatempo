import { renderHook } from '@testing-library/react'
import { act } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { AlertPorts } from '../lib/alerts'
import { memoryStorage } from '../test/memoryStorage'
import { createTimerStore, type TimerStore } from './useTimerStore'
import { useCycleEndAlerts } from './useCycleEndAlerts'

const MINUTE = 60_000
const START = new Date(2026, 8, 2, 10, 0, 0).getTime()

function fakeAlerts(overrides: Partial<AlertPorts> = {}): AlertPorts {
  return {
    prepare: vi.fn(),
    playAlarm: vi.fn(),
    notify: vi.fn(),
    isHidden: () => false,
    ...overrides,
  }
}

function setup(alerts: AlertPorts): { store: TimerStore; unmount: () => void } {
  const store = createTimerStore({ storage: memoryStorage() })
  const { unmount } = renderHook(() => useCycleEndAlerts(store, alerts))

  return { store, unmount }
}

/** Leva um ciclo de foco até o fim. */
function runFocusToEnd(store: TimerStore): void {
  act(() => store.getState().start())
  vi.setSystemTime(START + 25 * MINUTE)
  act(() => store.getState().sync())
}

beforeEach(() => {
  vi.useFakeTimers()
  vi.setSystemTime(START)
})

afterEach(() => {
  vi.useRealTimers()
})

describe('alarme', () => {
  it('deve tocar ao fim do foco', () => {
    const alerts = fakeAlerts()
    const { store } = setup(alerts)

    runFocusToEnd(store)

    expect(alerts.playAlarm).toHaveBeenCalledTimes(1)
  })

  it('deve tocar ao fim da pausa', () => {
    const alerts = fakeAlerts()
    const { store } = setup(alerts)
    runFocusToEnd(store)
    act(() => store.getState().acknowledge())
    act(() => store.getState().start())

    vi.setSystemTime(START + 31 * MINUTE)
    act(() => store.getState().sync())

    expect(alerts.playAlarm).toHaveBeenCalledTimes(2)
  })

  it('não deve tocar ao iniciar um ciclo', () => {
    const alerts = fakeAlerts()
    const { store } = setup(alerts)

    act(() => store.getState().start())

    expect(alerts.playAlarm).not.toHaveBeenCalled()
  })

  it('não deve tocar ao abandonar', () => {
    const alerts = fakeAlerts()
    const { store } = setup(alerts)
    act(() => store.getState().start())

    vi.setSystemTime(START + 20 * MINUTE)
    act(() => store.getState().abandon())

    expect(alerts.playAlarm).not.toHaveBeenCalled()
  })

  it('não deve tocar de novo a cada sincronização depois do fim', () => {
    const alerts = fakeAlerts()
    const { store } = setup(alerts)
    runFocusToEnd(store)

    vi.setSystemTime(START + 40 * MINUTE)
    act(() => store.getState().sync())

    expect(alerts.playAlarm).toHaveBeenCalledTimes(1)
  })
})

describe('notificação', () => {
  it('deve notificar o fim do foco quando a aba está em segundo plano', () => {
    const alerts = fakeAlerts({ isHidden: () => true })
    const { store } = setup(alerts)

    runFocusToEnd(store)

    expect(alerts.notify).toHaveBeenCalledWith({
      title: 'Pomodoro concluído',
      body: 'Hora da pausa.',
    })
  })

  it('deve notificar o fim da pausa com mensagem própria', () => {
    const alerts = fakeAlerts({ isHidden: () => true })
    const { store } = setup(alerts)
    runFocusToEnd(store)
    act(() => store.getState().acknowledge())
    act(() => store.getState().start())

    vi.setSystemTime(START + 31 * MINUTE)
    act(() => store.getState().sync())

    expect(alerts.notify).toHaveBeenLastCalledWith({
      title: 'Pausa concluída',
      body: 'Hora de voltar ao foco.',
    })
  })

  it('não deve notificar com a aba visível — o alarme já basta', () => {
    const alerts = fakeAlerts({ isHidden: () => false })
    const { store } = setup(alerts)

    runFocusToEnd(store)

    expect(alerts.notify).not.toHaveBeenCalled()
    expect(alerts.playAlarm).toHaveBeenCalledTimes(1)
  })
})

describe('ciclo de vida', () => {
  it('deve cancelar a inscrição ao desmontar', () => {
    const alerts = fakeAlerts()
    const { store, unmount } = setup(alerts)

    unmount()
    runFocusToEnd(store)

    expect(alerts.playAlarm).not.toHaveBeenCalled()
  })
})
