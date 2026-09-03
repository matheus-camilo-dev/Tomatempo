import { describe, expect, it } from 'vitest'
import {
  DEFAULT_LANGUAGE,
  LANGUAGE_STORAGE_KEY,
  SUPPORTED_LANGUAGES,
  createI18n,
  resources,
} from './index'

/** Achata `{a:{b:'x'}}` em `['a.b']` para comparar catálogos chave a chave. */
function flatKeys(value: object, prefix = ''): string[] {
  return Object.entries(value).flatMap(([key, child]) =>
    typeof child === 'object' && child !== null
      ? flatKeys(child as object, `${prefix}${key}.`)
      : [`${prefix}${key}`],
  )
}

describe('catálogos', () => {
  it('deve suportar português e inglês', () => {
    expect(SUPPORTED_LANGUAGES).toEqual(['pt-BR', 'en'])
  })

  it('deve ter exatamente as mesmas chaves nos dois idiomas', () => {
    const [ptKeys, enKeys] = [
      flatKeys(resources['pt-BR'].translation).sort(),
      flatKeys(resources.en.translation).sort(),
    ]

    expect(enKeys).toEqual(ptKeys)
  })

  it('não deve ter valor vazio', () => {
    for (const language of SUPPORTED_LANGUAGES) {
      const values = flatKeys(resources[language].translation).map((key) =>
        key.split('.').reduce<unknown>((node, part) => (node as Record<string, unknown>)[part], resources[language].translation),
      )

      expect(values.every((value) => typeof value === 'string' && value.trim().length > 0)).toBe(true)
    }
  })
})

describe('createI18n', () => {
  it('deve traduzir para o idioma pedido', async () => {
    const i18n = await createI18n({ language: 'en' })

    expect(i18n.t('action.abandon')).toBe('Stop')
  })

  it('deve traduzir para português', async () => {
    const i18n = await createI18n({ language: 'pt-BR' })

    expect(i18n.t('action.abandon')).toBe('Abandonar')
  })

  it('deve cair no idioma padrão quando o pedido não é suportado', async () => {
    const i18n = await createI18n({ language: 'fr' })

    expect(i18n.t('action.abandon')).toBe(resources[DEFAULT_LANGUAGE].translation.action.abandon)
  })

  it('deve tratar pt e pt-PT como português do Brasil', async () => {
    const i18n = await createI18n({ language: 'pt' })

    expect(i18n.t('action.abandon')).toBe('Abandonar')
  })

  it('deve detectar o idioma do navegador quando nenhum é pedido', async () => {
    const original = Object.getOwnPropertyDescriptor(globalThis.navigator, 'language')
    Object.defineProperty(globalThis.navigator, 'language', {
      configurable: true,
      get: () => 'en-US',
    })

    try {
      const i18n = await createI18n()

      expect(i18n.resolvedLanguage).toBe('en')
    } finally {
      if (original) Object.defineProperty(globalThis.navigator, 'language', original)
    }
  })

  it('deve guardar a escolha do usuário para o próximo acesso', async () => {
    const i18n = await createI18n({ language: 'pt-BR' })

    await i18n.changeLanguage('en')

    expect(localStorage.getItem(LANGUAGE_STORAGE_KEY)).toBe('en')
  })

  it('deve pluralizar o resumo do dia em português', async () => {
    const i18n = await createI18n({ language: 'pt-BR' })

    expect(i18n.t('today.summary', { count: 1, minutes: 25 })).toContain('1 pomodoro ')
    expect(i18n.t('today.summary', { count: 4, minutes: 100 })).toContain('4 pomodoros')
  })

  it('deve pluralizar o resumo do dia em inglês', async () => {
    const i18n = await createI18n({ language: 'en' })

    expect(i18n.t('today.summary', { count: 1, minutes: 25 })).toContain('1 pomodoro ')
    expect(i18n.t('today.summary', { count: 0, minutes: 0 })).toContain('0 pomodoros')
  })
})
