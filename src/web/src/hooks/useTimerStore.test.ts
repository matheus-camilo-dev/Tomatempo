import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createTimerStore, selectTodayTotals } from './useTimerStore'

const MINUTE = 60_000

/** Storage isolado por teste — nada vaza entre casos nem para o localStorage real. */
function memoryStorage(seed: Record<string, string> = {}): Storage {
  const data = new Map(Object.entries(seed))

  return {
    get length() {
      return data.size
    },
    clear: () => data.clear(),
    getItem: (key: string) => data.get(key) ?? null,
    key: (index: number) => [...data.keys()][index] ?? null,
    removeItem: (key: string) => void data.delete(key),
    setItem: (key: string, value: string) => void data.set(key, value),
  }
}

/** 2 de setembro de 2026, 10:00 local. */
const START = new Date(2026, 8, 2, 10, 0, 0).getTime()

beforeEach(() => {
  vi.useFakeTimers()
  vi.setSystemTime(START)
})

afterEach(() => {
  vi.useRealTimers()
})

describe('estado inicial', () => {
  it('deve começar ocioso, em foco e com o dia zerado', () => {
    const store = createTimerStore({ storage: memoryStorage() })
    const state = store.getState()

    expect(state.timer.status).toBe('idle')
    expect(state.timer.kind).toBe('focus')
    expect(selectTodayTotals(state)).toEqual({ focusMinutes: 0, completedPomodoros: 0 })
  })
})

describe('start', () => {
  it('deve iniciar a contagem a partir do relógio', () => {
    const store = createTimerStore({ storage: memoryStorage() })

    store.getState().start()

    expect(store.getState().timer.status).toBe('running')
    expect(store.getState().timer.endsAt).toBe(START + 25 * MINUTE)
  })
})

describe('sync', () => {
  it('deve manter em andamento antes do fim', () => {
    const store = createTimerStore({ storage: memoryStorage() })
    store.getState().start()

    vi.setSystemTime(START + 10 * MINUTE)
    store.getState().sync()

    expect(store.getState().timer.status).toBe('running')
    expect(store.getState().remainingMs).toBe(15 * MINUTE)
  })

  it('deve encerrar o ciclo e creditar o pomodoro completo', () => {
    const store = createTimerStore({ storage: memoryStorage() })
    store.getState().start()

    vi.setSystemTime(START + 25 * MINUTE)
    store.getState().sync()

    expect(store.getState().timer.status).toBe('finished')
    expect(selectTodayTotals(store.getState())).toEqual({
      focusMinutes: 25,
      completedPomodoros: 1,
    })
  })

  it('não deve creditar duas vezes o mesmo ciclo', () => {
    const store = createTimerStore({ storage: memoryStorage() })
    store.getState().start()

    vi.setSystemTime(START + 25 * MINUTE)
    store.getState().sync()
    vi.setSystemTime(START + 40 * MINUTE)
    store.getState().sync()

    expect(selectTodayTotals(store.getState()).focusMinutes).toBe(25)
  })

  it('não deve creditar minutos ao fim de uma pausa', () => {
    const store = createTimerStore({ storage: memoryStorage() })
    store.getState().start()
    vi.setSystemTime(START + 25 * MINUTE)
    store.getState().sync()
    store.getState().acknowledge()
    store.getState().start()

    vi.setSystemTime(START + 31 * MINUTE)
    store.getState().sync()

    expect(selectTodayTotals(store.getState()).focusMinutes).toBe(25)
  })
})

describe('acknowledge', () => {
  it('deve deixar a pausa pronta, porém parada', () => {
    const store = createTimerStore({ storage: memoryStorage() })
    store.getState().start()
    vi.setSystemTime(START + 25 * MINUTE)
    store.getState().sync()

    store.getState().acknowledge()

    expect(store.getState().timer.status).toBe('idle')
    expect(store.getState().timer.kind).toBe('shortBreak')
  })
})

describe('abandon', () => {
  it('deve creditar o parcial ao abandonar no minuto 20', () => {
    const store = createTimerStore({ storage: memoryStorage() })
    store.getState().start()

    vi.setSystemTime(START + 20 * MINUTE)
    store.getState().abandon()

    expect(selectTodayTotals(store.getState())).toEqual({
      focusMinutes: 20,
      completedPomodoros: 0,
    })
    expect(store.getState().timer.status).toBe('idle')
  })

  it('deve descartar parcial abaixo de 5 minutos', () => {
    const store = createTimerStore({ storage: memoryStorage() })
    store.getState().start()

    vi.setSystemTime(START + 3 * MINUTE)
    store.getState().abandon()

    expect(selectTodayTotals(store.getState()).focusMinutes).toBe(0)
  })
})

describe('persistência', () => {
  it('deve restaurar o ciclo em andamento com o tempo correto após recarregar', () => {
    const storage = memoryStorage()
    createTimerStore({ storage }).getState().start()

    // Recarregou a página 10 minutos depois.
    vi.setSystemTime(START + 10 * MINUTE)
    const restored = createTimerStore({ storage })

    expect(restored.getState().timer.status).toBe('running')
    expect(restored.getState().remainingMs).toBe(15 * MINUTE)
  })

  it('deve encerrar e creditar um ciclo que terminou com a aba fechada', () => {
    const storage = memoryStorage()
    createTimerStore({ storage }).getState().start()

    // Voltou só uma hora depois: o ciclo terminou enquanto a aba estava fechada.
    vi.setSystemTime(START + 60 * MINUTE)
    const restored = createTimerStore({ storage })

    expect(restored.getState().timer.status).toBe('finished')
    expect(selectTodayTotals(restored.getState())).toEqual({
      focusMinutes: 25,
      completedPomodoros: 1,
    })
  })

  it('deve preservar os totais do dia entre recarregamentos', () => {
    const storage = memoryStorage()
    const first = createTimerStore({ storage })
    first.getState().start()
    vi.setSystemTime(START + 20 * MINUTE)
    first.getState().abandon()

    const restored = createTimerStore({ storage })

    expect(selectTodayTotals(restored.getState()).focusMinutes).toBe(20)
  })

  it('deve zerar os totais quando o dia local virou', () => {
    const storage = memoryStorage()
    const first = createTimerStore({ storage })
    first.getState().start()
    vi.setSystemTime(START + 25 * MINUTE)
    first.getState().sync()

    // Dia seguinte, mesma máquina.
    vi.setSystemTime(new Date(2026, 8, 3, 9, 0, 0).getTime())
    const nextDay = createTimerStore({ storage })

    expect(selectTodayTotals(nextDay.getState())).toEqual({
      focusMinutes: 0,
      completedPomodoros: 0,
    })
  })

  it('deve partir de um estado limpo quando o armazenamento está corrompido', () => {
    const storage = memoryStorage({ 'tomatempo:timer:v1': '{isso não é json' })

    const store = createTimerStore({ storage })

    expect(store.getState().timer.status).toBe('idle')
    expect(selectTodayTotals(store.getState()).focusMinutes).toBe(0)
  })

  it('deve funcionar sem armazenamento disponível (navegação privada)', () => {
    const store = createTimerStore({ storage: null })

    store.getState().start()
    vi.setSystemTime(START + 25 * MINUTE)
    store.getState().sync()

    expect(selectTodayTotals(store.getState()).completedPomodoros).toBe(1)
  })

  it('deve continuar contando quando a gravação falha por cota estourada', () => {
    const storage = memoryStorage()
    storage.setItem = () => {
      throw new DOMException('QuotaExceededError')
    }
    const store = createTimerStore({ storage })

    store.getState().start()
    vi.setSystemTime(START + 25 * MINUTE)
    store.getState().sync()

    expect(store.getState().timer.status).toBe('finished')
    expect(selectTodayTotals(store.getState()).completedPomodoros).toBe(1)
  })

  it('deve descartar dado salvo em formato desconhecido', () => {
    // JSON válido, formato errado: acontece quando o formato salvo muda de versão.
    const storage = memoryStorage({ 'tomatempo:timer:v1': '{"timer":"formato antigo"}' })

    const store = createTimerStore({ storage })

    expect(store.getState().timer.status).toBe('idle')
    expect(store.getState().timer.kind).toBe('focus')
  })

  it('deve descartar payload nulo no armazenamento', () => {
    const storage = memoryStorage({ 'tomatempo:timer:v1': 'null' })

    expect(createTimerStore({ storage }).getState().timer.status).toBe('idle')
  })

  it('deve sobreviver a navegador que lança ao acessar o localStorage', () => {
    // Safari em navegação privada e navegadores com cookies bloqueados chegam a
    // lançar no *acesso* à propriedade, não só na escrita.
    const original = Object.getOwnPropertyDescriptor(globalThis, 'localStorage')
    Object.defineProperty(globalThis, 'localStorage', {
      configurable: true,
      get() {
        throw new DOMException('SecurityError')
      },
    })

    try {
      expect(() => createTimerStore()).not.toThrow()
      expect(createTimerStore().getState().timer.status).toBe('idle')
    } finally {
      if (original) Object.defineProperty(globalThis, 'localStorage', original)
    }
  })

  it('deve usar o localStorage do navegador quando nenhum armazenamento é informado', () => {
    localStorage.clear()

    createTimerStore().getState().start()
    vi.setSystemTime(START + 10 * MINUTE)

    expect(createTimerStore().getState().remainingMs).toBe(15 * MINUTE)

    localStorage.clear()
  })
})
