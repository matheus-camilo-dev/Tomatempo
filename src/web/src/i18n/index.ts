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
      nav: {
        account: 'Minha conta',
        signIn: 'Entrar',
        signUp: 'Criar conta',
      },
      account: {
        loading: 'Carregando sua conta…',
        emailLabel: 'E-mail',
        passwordLabel: 'Senha',
        newPasswordLabel: 'Nova senha',
        nicknameLabel: 'Apelido',
        goalLabel: 'Meta diária, em pomodoros',
        timeZoneLabel: 'Fuso horário',
        signUpTitle: 'Criar conta',
        signUpSubmit: 'Criar conta',
        signInTitle: 'Entrar',
        signInSubmit: 'Entrar',
        signInWithGoogle: 'Entrar com o Google',
        forgotLink: 'Esqueci minha senha',
        haveAccount: 'Já tem conta?',
        noAccount: 'Ainda não tem conta?',
        signOut: 'Sair',
        checkYourEmail: 'Se o endereço for novo, a conta foi criada. Confira seu e-mail.',
        unconfirmedEmail: 'Seu e-mail ainda não foi confirmado. Sem isso, você fica fora do ranking.',
        resendConfirmation: 'Reenviar confirmação',
        resendRequested: 'Se o endereço tiver conta, o link acabou de sair.',
        onboardingTitle: 'Falta pouco',
        onboardingSubmit: 'Concluir',
        title: 'Minha conta',
        goalCurrentLabel: 'Meta de hoje',
        goalUpcoming: 'Nova meta de {{pomodoros}} a partir de {{date}}',
        saveGoal: 'Salvar meta',
        streakLabel: 'Sequência atual, em dias',
        freezesLabel: 'Congelamentos disponíveis',
        freezesTitle: 'De onde vieram',
        deleteTitle: 'Excluir conta',
        deleteSubmit: 'Excluir minha conta',
        deletePasswordLabel: 'Confirme sua senha',
        forgotTitle: 'Recuperar acesso',
        forgotSubmit: 'Enviar link',
        forgotRequested: 'Se o endereço tiver conta, o link acabou de sair.',
        resetTitle: 'Escolher nova senha',
        resetSubmit: 'Trocar senha',
        confirmingEmail: 'Confirmando seu e-mail…',
        emailConfirmed: 'E-mail confirmado.',
      },
      freeze: {
        reason: {
          Signup: 'Cadastro',
          Milestone: 'Sete dias seguidos de meta batida',
          RankingReward: 'Prêmio do ranking semanal',
          DayCovered: 'Dia coberto',
          CapOverflow: 'Acima do teto',
          unknown: 'Outro motivo',
        },
      },
      error: {
        unauthenticated: 'Sua sessão terminou. Entre de novo.',
        forbidden: 'Você não tem acesso a isso.',
        invalid_request: 'Confira os dados e tente de novo.',
        invalid_credentials: 'E-mail ou senha não conferem.',
        invalid_email: 'Esse e-mail não parece válido.',
        invalid_nickname: 'Apelido de 3 a 20 caracteres, só letras e números.',
        nickname_taken: 'Esse apelido já é de outra pessoa.',
        nickname_locked: 'O apelido só pode ser trocado a cada 30 dias.',
        invalid_goal: 'Escolha uma meta entre 1 e 24 pomodoros.',
        invalid_time_zone: 'Esse fuso horário não existe.',
        invalid_locale: 'Idioma não suportado.',
        invalid_color_mode: 'Modo de cor inválido.',
        weak_password: 'Essa senha é curta demais ou comum demais. Use pelo menos 8 caracteres.',
        invalid_token: 'Esse link não vale mais. Peça outro.',
        onboarding_required: 'Complete sua conta antes de continuar.',
        onboarding_already_completed: 'Sua conta já está completa.',
        too_many_requests: 'Tentativas demais. Espere um minuto.',
        unsupported_media_type: 'Formato de envio não aceito.',
        internal_error: 'Algo deu errado do nosso lado. Tente de novo.',
        external_login_failed: 'Não deu para entrar pelo Google. Tente de novo.',
        external_email_not_verified: 'O Google não confirmou esse endereço, então não dá para entrar por ele.',
        network: 'Sem conexão com o servidor.',
        unknown: 'Algo deu errado. Tente de novo.',
      },
      language: {
        group: 'Idioma',
        ptBR: 'Português',
        en: 'English',
      },
      offline: {
        banner: 'Você está offline. O timer continua contando e o dia é creditado; minutos de ranking exigem conexão.',
      },
      theme: {
        group: 'Tema',
        system: 'Sistema',
        light: 'Claro',
        dark: 'Escuro',
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
      nav: {
        account: 'My account',
        signIn: 'Sign in',
        signUp: 'Create account',
      },
      account: {
        loading: 'Loading your account…',
        emailLabel: 'Email',
        passwordLabel: 'Password',
        newPasswordLabel: 'New password',
        nicknameLabel: 'Nickname',
        goalLabel: 'Daily goal, in pomodoros',
        timeZoneLabel: 'Time zone',
        signUpTitle: 'Create account',
        signUpSubmit: 'Create account',
        signInTitle: 'Sign in',
        signInSubmit: 'Sign in',
        signInWithGoogle: 'Sign in with Google',
        forgotLink: 'I forgot my password',
        haveAccount: 'Already have an account?',
        noAccount: 'No account yet?',
        signOut: 'Sign out',
        checkYourEmail: 'If the address is new, the account was created. Check your email.',
        unconfirmedEmail: 'Your email is not confirmed yet. Until it is, you stay out of the ranking.',
        resendConfirmation: 'Resend confirmation',
        resendRequested: 'If the address has an account, the link just went out.',
        onboardingTitle: 'Almost there',
        onboardingSubmit: 'Finish',
        title: 'My account',
        goalCurrentLabel: 'Goal for today',
        goalUpcoming: 'New goal of {{pomodoros}} starting {{date}}',
        saveGoal: 'Save goal',
        streakLabel: 'Current streak, in days',
        freezesLabel: 'Freezes available',
        freezesTitle: 'Where they came from',
        deleteTitle: 'Delete account',
        deleteSubmit: 'Delete my account',
        deletePasswordLabel: 'Confirm your password',
        forgotTitle: 'Recover access',
        forgotSubmit: 'Send link',
        forgotRequested: 'If the address has an account, the link just went out.',
        resetTitle: 'Choose a new password',
        resetSubmit: 'Change password',
        confirmingEmail: 'Confirming your email…',
        emailConfirmed: 'Email confirmed.',
      },
      freeze: {
        reason: {
          Signup: 'Sign-up',
          Milestone: 'Seven days in a row on goal',
          RankingReward: 'Weekly ranking reward',
          DayCovered: 'Day covered',
          CapOverflow: 'Over the cap',
          unknown: 'Other reason',
        },
      },
      error: {
        unauthenticated: 'Your session ended. Sign in again.',
        forbidden: 'You do not have access to that.',
        invalid_request: 'Check the fields and try again.',
        invalid_credentials: 'Email or password do not match.',
        invalid_email: 'That email does not look valid.',
        invalid_nickname: 'Nickname from 3 to 20 characters, letters and digits only.',
        nickname_taken: 'That nickname belongs to someone else.',
        nickname_locked: 'The nickname can only change every 30 days.',
        invalid_goal: 'Pick a goal between 1 and 24 pomodoros.',
        invalid_time_zone: 'That time zone does not exist.',
        invalid_locale: 'Language not supported.',
        invalid_color_mode: 'Invalid color mode.',
        weak_password: 'Password too short or too common. Use at least 8 characters.',
        invalid_token: 'That link is no longer valid. Ask for another.',
        onboarding_required: 'Finish setting up your account first.',
        onboarding_already_completed: 'Your account is already complete.',
        too_many_requests: 'Too many attempts. Wait a minute.',
        unsupported_media_type: 'Submission format not accepted.',
        internal_error: 'Something went wrong on our side. Try again.',
        external_login_failed: 'Could not sign in with Google. Try again.',
        external_email_not_verified: 'Google did not confirm that address, so it cannot be used to sign in.',
        network: 'No connection to the server.',
        unknown: 'Something went wrong. Try again.',
      },
      language: {
        group: 'Language',
        ptBR: 'Português',
        en: 'English',
      },
      offline: {
        banner: 'You are offline. The timer keeps running and your day is credited; ranking minutes require a connection.',
      },
      theme: {
        group: 'Theme',
        system: 'System',
        light: 'Light',
        dark: 'Dark',
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
