import { renderHook } from '@testing-library/react'
import { act } from 'react'
import { afterEach, describe, expect, it } from 'vitest'
import { useOnlineStatus } from './useOnlineStatus'

/** Controla `navigator.onLine`, que é somente leitura em jsdom. */
function setOnline(value: boolean): void {
  Object.defineProperty(globalThis.navigator, 'onLine', { configurable: true, value })
}

afterEach(() => setOnline(true))

describe('useOnlineStatus', () => {
  it('deve começar com o estado atual da conexão', () => {
    setOnline(false)

    const { result } = renderHook(() => useOnlineStatus())

    expect(result.current).toBe(false)
  })

  it('deve reagir à perda de conexão', () => {
    setOnline(true)
    const { result } = renderHook(() => useOnlineStatus())

    act(() => {
      setOnline(false)
      globalThis.dispatchEvent(new Event('offline'))
    })

    expect(result.current).toBe(false)
  })

  it('deve reagir à volta da conexão', () => {
    setOnline(false)
    const { result } = renderHook(() => useOnlineStatus())

    act(() => {
      setOnline(true)
      globalThis.dispatchEvent(new Event('online'))
    })

    expect(result.current).toBe(true)
  })

  it('deve parar de ouvir ao desmontar', () => {
    const { result, unmount } = renderHook(() => useOnlineStatus())
    unmount()

    act(() => {
      setOnline(false)
      globalThis.dispatchEvent(new Event('offline'))
    })

    expect(result.current).toBe(true)
  })
})
