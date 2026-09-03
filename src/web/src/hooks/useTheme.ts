import { useCallback, useEffect, useState } from 'react'
import {
  applyTheme,
  readPreference,
  resolveTheme,
  writePreference,
  type ResolvedTheme,
  type ThemePreference,
} from '../lib/theme'

const DARK_QUERY = '(prefers-color-scheme: dark)'

export interface UseThemeOptions {
  readonly media?: MediaQueryList
  readonly storage?: Storage | null
  readonly root?: HTMLElement
}

export interface UseThemeResult {
  readonly preference: ThemePreference
  readonly resolved: ResolvedTheme
  setPreference(preference: ThemePreference): void
}

export function useTheme(options: UseThemeOptions = {}): UseThemeResult {
  const media = options.media ?? globalThis.matchMedia(DARK_QUERY)
  const storage = options.storage === undefined ? globalThis.localStorage : options.storage
  const root = options.root ?? document.documentElement

  const [preference, setPreferenceState] = useState<ThemePreference>(() => readPreference(storage))
  const [prefersDark, setPrefersDark] = useState(() => media.matches)

  // Enquanto a preferência for "system", o tema acompanha o sistema em tempo
  // real — trocar o tema do SO muda a tela sem recarregar.
  useEffect(() => {
    const onChange = (event: MediaQueryListEvent) => setPrefersDark(event.matches)

    media.addEventListener('change', onChange)

    return () => media.removeEventListener('change', onChange)
  }, [media])

  const resolved = resolveTheme(preference, prefersDark)

  useEffect(() => {
    applyTheme(root, resolved)
  }, [root, resolved])

  const setPreference = useCallback(
    (next: ThemePreference) => {
      writePreference(storage, next)
      setPreferenceState(next)
    },
    [storage],
  )

  return { preference, resolved, setPreference }
}
