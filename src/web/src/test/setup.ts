import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'
import { DEFAULT_LANGUAGE, LANGUAGE_STORAGE_KEY, initGlobalI18n } from '../i18n'

// A instância global precisa existir antes do primeiro render: `useTranslation`
// sem provider recorre a ela, e sem inicialização a tela mostraria as chaves.
const i18n = await initGlobalI18n({ language: DEFAULT_LANGUAGE })

// jsdom não implementa matchMedia. Isto é limitação do ambiente de teste, não
// defesa de produção (§6.8) — no navegador a API existe em todo lugar.
if (!globalThis.matchMedia) {
  globalThis.matchMedia = ((query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addEventListener: () => {},
    removeEventListener: () => {},
    addListener: () => {},
    removeListener: () => {},
    dispatchEvent: () => false,
  })) as typeof globalThis.matchMedia
}

afterEach(async () => {
  cleanup()
  // Idioma é estado global: sem restaurar, um teste que troca para inglês
  // contamina os seguintes.
  localStorage.removeItem(LANGUAGE_STORAGE_KEY)
  if (i18n.language !== DEFAULT_LANGUAGE) await i18n.changeLanguage(DEFAULT_LANGUAGE)
})
