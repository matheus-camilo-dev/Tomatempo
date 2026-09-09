import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { DEFAULT_LANGUAGE, initGlobalI18n } from '../i18n'
import { server } from './server'

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

// `error` e não `bypass`: requisição que nenhum teste declarou é chamada à API
// que ninguém esperava — e é exatamente isso que a fase 2 precisa flagrar.
beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
afterAll(() => server.close())

afterEach(async () => {
  cleanup()
  server.resetHandlers()
  // Tudo que o app guarda é estado global: idioma, tema e a pista de sessão.
  // Sem limpar, o teste que troca para inglês, escurece a tela ou faz login
  // contamina os seguintes. Limpar por prefixo cobre o que ainda vier.
  for (const chave of Object.keys(localStorage)) {
    if (chave.startsWith('tomatempo:')) localStorage.removeItem(chave)
  }
  document.documentElement.classList.remove('dark')
  if (i18n.language !== DEFAULT_LANGUAGE) await i18n.changeLanguage(DEFAULT_LANGUAGE)
})
