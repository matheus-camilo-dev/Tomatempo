import { renderHook } from '@testing-library/react'
import { act } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createTimerStore } from './useTimerStore'
import { TICK_INTERVAL_MS, useTimer } from './useTimer'

const MINUTE = 60_000
const START = new Date(2026, 8, 2, 10, 0, 0).getTime()

function memoryStorage(): Storage {
  const data = new Map<string, string>()

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

beforeEach(() => {
  vi.useFakeTimers()
  vi.setSystemTime(START)
})

afterEach(() => {
  vi.useRealTimers()
})

describe('useTimer', () => {
  it('deve expor o estado atual do store', () => {
    const store = createTimerStore({ storage: memoryStorage() })

    const { result } = renderHook(() => useTimer(store))

    expect(result.current.timer.status).toBe('idle')
    expect(result.current.remainingMs).toBe(25 * MINUTE)
  })

  it('deve atualizar o tempo restante enquanto o ciclo corre', () => {
    const store = createTimerStore({ storage: memoryStorage() })
    const { result } = renderHook(() => useTimer(store))

    act(() => result.current.start())
    act(() => void vi.advanceTimersByTime(60_000))

    expect(result.current.remainingMs).toBe(24 * MINUTE)
  })

  it('deve encerrar o ciclo sozinho ao chegar no fim, sem emendar o próximo', () => {
    const store = createTimerStore({ storage: memoryStorage() })
    const { result } = renderHook(() => useTimer(store))

    act(() => result.current.start())
    act(() => void vi.advanceTimersByTime(25 * MINUTE))

    expect(result.current.timer.status).toBe('finished')
    expect(result.current.todayTotals.completedPomodoros).toBe(1)
  })

  it('deve ressincronizar ao voltar para a aba', () => {
    const store = createTimerStore({ storage: memoryStorage() })
    const { result } = renderHook(() => useTimer(store))
    act(() => result.current.start())

    // Aba em segundo plano: o navegador estrangula o intervalo, então o relógio
    // avança sem que nenhum tique rode.
    vi.setSystemTime(START + 10 * MINUTE)
    act(() => void document.dispatchEvent(new Event('visibilitychange')))

    expect(result.current.remainingMs).toBe(15 * MINUTE)
  })

  it('deve parar o intervalo ao desmontar', () => {
    const store = createTimerStore({ storage: memoryStorage() })
    const { result, unmount } = renderHook(() => useTimer(store))
    act(() => result.current.start())

    unmount()
    const pending = vi.getTimerCount()

    expect(pending).toBe(0)
  })

  it('deve tiquetaquear com folga para atualizar os segundos na tela', () => {
    expect(TICK_INTERVAL_MS).toBeLessThanOrEqual(500)
  })
})
