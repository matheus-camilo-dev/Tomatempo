/**
 * Tema claro/escuro — parte pura, sem React e sem navegador.
 *
 * Disponível para todos os usuários, logados ou não
 * (docs/REGRAS-DE-NEGOCIO.md §6). A galeria de presets da fase 2 se apoia
 * neste mesmo mecanismo.
 */

export const THEME_PREFERENCES = ['system', 'light', 'dark'] as const
export type ThemePreference = (typeof THEME_PREFERENCES)[number]

/** O que de fato é pintado na tela, já resolvido. */
export type ResolvedTheme = 'light' | 'dark'

export const THEME_STORAGE_KEY = 'tomatempo:theme'

export function resolveTheme(preference: ThemePreference, prefersDark: boolean): ResolvedTheme {
  if (preference !== 'system') return preference

  return prefersDark ? 'dark' : 'light'
}

export function readPreference(storage: Storage | null): ThemePreference {
  const stored = storage?.getItem(THEME_STORAGE_KEY)

  return isPreference(stored) ? stored : 'system'
}

/**
 * Guarda a escolha explícita e **apaga** ao voltar para "system": gravar
 * "system" registraria uma decisão que o usuário acabou de desfazer.
 */
export function writePreference(storage: Storage | null, preference: ThemePreference): void {
  if (!storage) return

  try {
    if (preference === 'system') storage.removeItem(THEME_STORAGE_KEY)
    else storage.setItem(THEME_STORAGE_KEY, preference)
  } catch {
    // Cota estourada ou storage bloqueado: o tema segue valendo nesta sessão.
  }
}

export function applyTheme(root: HTMLElement, theme: ResolvedTheme): void {
  root.classList.toggle('dark', theme === 'dark')
  // `color-scheme` alinha barras de rolagem, campos e menus nativos ao tema.
  root.style.colorScheme = theme
}

function isPreference(value: string | null | undefined): value is ThemePreference {
  return THEME_PREFERENCES.includes(value as ThemePreference)
}
