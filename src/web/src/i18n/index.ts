import i18next, { type i18n as I18nInstance } from 'i18next'
import LanguageDetector from 'i18next-browser-languagedetector'
import { initReactI18next } from 'react-i18next'

export const SUPPORTED_LANGUAGES = ['pt-BR', 'en'] as const
export type SupportedLanguage = (typeof SUPPORTED_LANGUAGES)[number]

/** Produto nasce brasileiro: o inglês é a alternativa, não a base. */
export const DEFAULT_LANGUAGE: SupportedLanguage = 'pt-BR'

export const LANGUAGE_STORAGE_KEY = 'tomatempo:language'

export const resources = {
  'pt-BR': {
    translation: {
      app: { name: 'Tomatempo' },
      cycle: {
        focus: 'Foco',
        shortBreak: 'Pausa curta',
        longBreak: 'Pausa longa',
        focusFinished: 'Pomodoro concluído',
        breakFinished: 'Pausa concluída',
        untilLongBreak: '{{count}} de {{total}} até a pausa longa',
      },
      action: {
        startFocus: 'Iniciar foco',
        startShortBreak: 'Iniciar pausa curta',
        startLongBreak: 'Iniciar pausa longa',
        abandon: 'Abandonar',
      },
      timer: { remaining: 'Tempo restante' },
      today: {
        // `_zero` é sobrescrita explícita: o CLDR classifica 0 como "one" em
        // português, o que renderiza "0 pomodoro" — correto pela regra, errado
        // para quem lê em português do Brasil.
        summary_zero: '{{count}} pomodoros · {{minutes}} min de foco hoje',
        summary_one: '{{count}} pomodoro · {{minutes}} min de foco hoje',
        summary_other: '{{count}} pomodoros · {{minutes}} min de foco hoje',
      },
      notification: {
        focusEndedTitle: 'Pomodoro concluído',
        focusEndedBody: 'Hora da pausa.',
        breakEndedTitle: 'Pausa concluída',
        breakEndedBody: 'Hora de voltar ao foco.',
      },
      cta: 'Crie uma conta para acompanhar sua sequência de dias e disputar o ranking semanal.',
      language: {
        group: 'Idioma',
        ptBR: 'Português',
        en: 'English',
      },
    },
  },
  en: {
    translation: {
      app: { name: 'Tomatempo' },
      cycle: {
        focus: 'Focus',
        shortBreak: 'Short break',
        longBreak: 'Long break',
        focusFinished: 'Pomodoro complete',
        breakFinished: 'Break complete',
        untilLongBreak: '{{count}} of {{total}} until the long break',
      },
      action: {
        startFocus: 'Start focus',
        startShortBreak: 'Start short break',
        startLongBreak: 'Start long break',
        abandon: 'Stop',
      },
      timer: { remaining: 'Time remaining' },
      today: {
        summary_zero: '{{count}} pomodoros · {{minutes}} min focused today',
        summary_one: '{{count}} pomodoro · {{minutes}} min focused today',
        summary_other: '{{count}} pomodoros · {{minutes}} min focused today',
      },
      notification: {
        focusEndedTitle: 'Pomodoro complete',
        focusEndedBody: 'Time for a break.',
        breakEndedTitle: 'Break complete',
        breakEndedBody: 'Time to focus again.',
      },
      cta: 'Create an account to track your streak and join the weekly ranking.',
      language: {
        group: 'Language',
        ptBR: 'Português',
        en: 'English',
      },
    },
  },
} as const

export interface CreateI18nOptions {
  /** Idioma explícito; sem ele, detecta do navegador e do armazenamento. */
  readonly language?: string
}

export async function createI18n(options: CreateI18nOptions = {}): Promise<I18nInstance> {
  return initInstance(i18next.createInstance(), options)
}

/**
 * Configura a instância global do i18next — é a que `useTranslation` usa quando
 * não há provider, o que mantém componentes e hooks livres de encanamento.
 */
export async function initGlobalI18n(options: CreateI18nOptions = {}): Promise<I18nInstance> {
  return initInstance(i18next, options)
}

async function initInstance(
  instance: I18nInstance,
  options: CreateI18nOptions,
): Promise<I18nInstance> {
  await instance
    .use(LanguageDetector)
    .use(initReactI18next)
    .init({
      resources,
      supportedLngs: [...SUPPORTED_LANGUAGES],
      // Mapeamento explícito em vez de `nonExplicitSupportedLngs`: aquele
      // resolvia `pt-BR` para `pt`, que não tem catálogo, e a tela voltava
      // mostrando as chaves cruas.
      fallbackLng: { pt: ['pt-BR'], 'pt-PT': ['pt-BR'], default: [DEFAULT_LANGUAGE] },
      interpolation: { escapeValue: false },
      ...(options.language ? { lng: options.language } : {}),
      detection: {
        order: ['localStorage', 'navigator'],
        lookupLocalStorage: LANGUAGE_STORAGE_KEY,
        caches: ['localStorage'],
      },
    })

  return instance
}
