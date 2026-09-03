import { renderHook } from '@testing-library/react'
import { act } from 'react'
import { describe, expect, it } from 'vitest'
import { THEME_STORAGE_KEY } from '../lib/theme'
import { memoryStorage } from '../test/memoryStorage'
import { useTheme } from './useTheme'

/** MediaQueryList controlável — jsdom não implementa matchMedia de verdade. */
function fakeMedia(matches: boolean) {
  const listeners = new Set<(event: MediaQueryListEvent) => void>()

  const media = {
    get matches() {
      return media.current
    },
    current: matches,
    addEventListener: (_: string, listener: (event: MediaQueryListEvent) => void) =>
      void listeners.add(listener),
    removeEventListener: (_: string, listener: (event: MediaQueryListEvent) => void) =>
      void listeners.delete(listener),
  }

  return {
    media: media as unknown as MediaQueryList,
    /** Simula o usuário trocando o tema do sistema operacional. */
    change(next: boolean) {
      media.current = next
      for (const listener of listeners) listener({ matches: next } as MediaQueryListEvent)
    },
    get listenerCount() {
      return listeners.size
    },
  }
}

function setup(options: { prefersDark?: boolean; storage?: Storage } = {}) {
  const media = fakeMedia(options.prefersDark ?? false)
  const storage = options.storage ?? memoryStorage()
  const root = document.createElement('html')
  const view = renderHook(() => useTheme({ media: media.media, storage, root }))

  return { ...view, media, storage, root }
}

describe('sem escolha do usuário', () => {
  it('deve seguir o sistema no escuro', () => {
    const { result, root } = setup({ prefersDark: true })

    expect(result.current.preference).toBe('system')
    expect(result.current.resolved).toBe('dark')
    expect(root.classList.contains('dark')).toBe(true)
  })

  it('deve seguir o sistema no claro', () => {
    const { result, root } = setup({ prefersDark: false })

    expect(result.current.resolved).toBe('light')
    expect(root.classList.contains('dark')).toBe(false)
  })

  it('deve acompanhar a mudança do sistema sem recarregar', () => {
    const { result, media, root } = setup({ prefersDark: false })

    act(() => media.change(true))

    expect(result.current.resolved).toBe('dark')
    expect(root.classList.contains('dark')).toBe(true)
  })
})

describe('escolha explícita', () => {
  it('deve sobrepor o sistema', () => {
    const { result, root } = setup({ prefersDark: true })

    act(() => result.current.setPreference('light'))

    expect(result.current.resolved).toBe('light')
    expect(root.classList.contains('dark')).toBe(false)
  })

  it('deve ignorar a mudança do sistema enquanto durar', () => {
    const { result, media } = setup({ prefersDark: false })
    act(() => result.current.setPreference('light'))

    act(() => media.change(true))

    expect(result.current.resolved).toBe('light')
  })

  it('deve sobreviver ao recarregamento', () => {
    const storage = memoryStorage()
    const first = setup({ prefersDark: true, storage })
    act(() => first.result.current.setPreference('light'))
    first.unmount()

    const restored = setup({ prefersDark: true, storage })

    expect(restored.result.current.preference).toBe('light')
    expect(restored.result.current.resolved).toBe('light')
  })

  it('deve voltar a seguir o sistema ao escolher "system"', () => {
    const storage = memoryStorage({ [THEME_STORAGE_KEY]: 'light' })
    const { result, media } = setup({ prefersDark: true, storage })

    act(() => result.current.setPreference('system'))

    expect(result.current.resolved).toBe('dark')
    act(() => media.change(false))
    expect(result.current.resolved).toBe('light')
  })
})

describe('ciclo de vida', () => {
  it('deve parar de ouvir o sistema ao desmontar', () => {
    const { unmount, media } = setup()

    unmount()

    expect(media.listenerCount).toBe(0)
  })
})
