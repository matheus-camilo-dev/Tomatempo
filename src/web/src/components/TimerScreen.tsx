import { useTranslation } from 'react-i18next'
import type { CycleKind } from '../domain/timer'
import { POMODOROS_PER_LONG_BREAK, nextKind } from '../domain/timer'
import { useCycleEndAlerts } from '../hooks/useCycleEndAlerts'
import { useServerSession } from '../hooks/useServerSession'
import { useTheme } from '../hooks/useTheme'
import { timerStore, useTimer } from '../hooks/useTimer'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import type { TimerStore } from '../hooks/useTimerStore'
import { SUPPORTED_LANGUAGES } from '../i18n'
import { THEME_PREFERENCES } from '../lib/theme'
import { createBrowserAlerts, type AlertPorts } from '../lib/alerts'
import { formatDuration } from '../lib/formatDuration'

const browserAlerts = createBrowserAlerts()

const CYCLE_KEY: Record<CycleKind, string> = {
  focus: 'cycle.focus',
  shortBreak: 'cycle.shortBreak',
  longBreak: 'cycle.longBreak',
}

const START_KEY: Record<CycleKind, string> = {
  focus: 'action.startFocus',
  shortBreak: 'action.startShortBreak',
  longBreak: 'action.startLongBreak',
}

const LANGUAGE_KEY = { 'pt-BR': 'language.ptBR', en: 'language.en' } as const

const THEME_KEY = {
  system: 'theme.system',
  light: 'theme.light',
  dark: 'theme.dark',
} as const

const TOGGLE_CLASS =
  'rounded-full px-3 py-1 transition hover:bg-slate-200 aria-pressed:font-semibold aria-pressed:text-slate-900 dark:hover:bg-slate-800 dark:aria-pressed:text-slate-50'

export interface TimerScreenProps {
  /** Injetados nos testes; em produção usam as implementações padrão. */
  readonly store?: TimerStore
  readonly alerts?: AlertPorts
  /**
   * Desligado por quem já está dentro de uma rota: ali o CTA vira link do
   * roteador, e quem não tem conta continua vendo o mesmo texto.
   */
  readonly signupCta?: boolean
}

export function TimerScreen({
  store = timerStore,
  alerts = browserAlerts,
  signupCta = true,
}: TimerScreenProps) {
  const { t, i18n } = useTranslation()
  const { preference, setPreference } = useTheme()
  const online = useOnlineStatus()
  const { timer, remainingMs, todayTotals, start, abandon, acknowledge } = useTimer(store)
  const servidor = useServerSession(store)

  useCycleEndAlerts(store, alerts)

  // Logado, o dia é o da conta e vem só do servidor; o histórico do navegador
  // continua sendo o do anônimo e nunca ocupa o lugar dele — nem enquanto a
  // resposta não chega, nem com a rede fora (REGRAS §2).
  const hoje = servidor.signedIn ? servidor.today : todayTotals

  const isRunning = timer.status === 'running'
  const isFinished = timer.status === 'finished'
  // Ciclo encerrado mostra zero: `remainingMs` volta à duração cheia fora do
  // estado corrente, o que serve ao ocioso mas não a quem acabou de terminar.
  const displayMs = isFinished ? 0 : remainingMs
  const upcoming = isFinished ? nextKind(timer) : timer.kind

  function beginCycle() {
    // Destrava áudio e permissão aqui, no gesto: é a única janela em que o
    // navegador aceita, e o alarme só vai tocar 25 minutos depois.
    alerts.prepare()

    // O ciclo começa agora, pelo relógio local. Esperar o servidor deixaria o
    // contador parado durante a ida e volta da rede — e para quem não tem conta
    // não há ida e volta alguma (§5).
    start()

    const doCiclo = store.getState().timer.startedAt
    void servidor
      .openFocus()
      .then((janela) => janela && store.getState().adoptWindow(janela, doCiclo))
  }

  function startNext() {
    acknowledge()
    beginCycle()
  }

  function abandonCycle() {
    abandon()
    servidor.abandonFocus()
  }

  return (
    <main className="mx-auto flex min-h-screen max-w-md flex-col items-center justify-center gap-8 px-6 py-12 text-center">
      {!online && (
        // `role="alert"` e não `status`: a região de status já é do ciclo, e
        // dois elementos com o mesmo papel confundem leitor de tela e teste.
        <p
          role="alert"
          data-testid="offline-banner"
          className="rounded-lg bg-amber-100 px-4 py-2 text-sm text-amber-900 dark:bg-amber-900/40 dark:text-amber-100"
        >
          {t('offline.banner')}
        </p>
      )}

      <h1 className="text-xl font-semibold tracking-tight text-slate-500 dark:text-slate-400">
        {t('app.name')}
      </h1>

      <div className="flex flex-col items-center gap-3">
        <p
          role="status"
          aria-live="polite"
          className="text-sm font-medium uppercase tracking-widest text-slate-500 dark:text-slate-400"
        >
          {isFinished
            ? t(timer.kind === 'focus' ? 'cycle.focusFinished' : 'cycle.breakFinished')
            : t(CYCLE_KEY[timer.kind])}
        </p>

        {/*
          Sem `aria-live` aqui de propósito: um contador que fala a cada 250ms
          torna o leitor de tela inutilizável. Quem anuncia é a região acima,
          que muda só na virada de ciclo.
        */}
        <div
          role="timer"
          aria-label={t('timer.remaining')}
          className="font-mono text-7xl font-bold tabular-nums text-slate-900 dark:text-slate-50"
        >
          {formatDuration(displayMs)}
        </div>

        <p className="text-sm text-slate-500 dark:text-slate-400">
          {t('cycle.untilLongBreak', {
            count: timer.completedPomodoros % POMODOROS_PER_LONG_BREAK,
            total: POMODOROS_PER_LONG_BREAK,
          })}
        </p>
      </div>

      {isRunning ? (
        <button
          type="button"
          onClick={abandonCycle}
          className="rounded-full border border-slate-300 px-8 py-3 text-base font-medium text-slate-700 transition hover:bg-slate-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-slate-900 dark:border-slate-600 dark:text-slate-200 dark:hover:bg-slate-800"
        >
          {t('action.abandon')}
        </button>
      ) : (
        <button
          type="button"
          onClick={isFinished ? startNext : beginCycle}
          className="rounded-full bg-slate-900 px-8 py-3 text-base font-medium text-white transition hover:bg-slate-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-slate-900 dark:bg-slate-50 dark:text-slate-900 dark:hover:bg-slate-200"
        >
          {t(START_KEY[upcoming])}
        </button>
      )}

      <p data-testid="today-summary" className="text-sm text-slate-600 dark:text-slate-300">
        {hoje &&
          t('today.summary', {
            count: hoje.completedPomodoros,
            minutes: hoje.focusMinutes,
          })}
      </p>

      {signupCta && (
        <p
          data-testid="signup-cta"
          className="max-w-xs text-sm leading-relaxed text-slate-500 dark:text-slate-400"
        >
          {t('cta')}
        </p>
      )}

      <div
        role="group"
        aria-label={t('language.group')}
        className="flex gap-1 text-xs text-slate-500 dark:text-slate-400"
      >
        {SUPPORTED_LANGUAGES.map((language) => (
          <button
            key={language}
            type="button"
            aria-pressed={i18n.resolvedLanguage === language}
            onClick={() => void i18n.changeLanguage(language)}
            className={TOGGLE_CLASS}
          >
            {t(LANGUAGE_KEY[language])}
          </button>
        ))}
      </div>

      <div
        role="group"
        aria-label={t('theme.group')}
        className="flex gap-1 text-xs text-slate-500 dark:text-slate-400"
      >
        {THEME_PREFERENCES.map((option) => (
          <button
            key={option}
            type="button"
            aria-pressed={preference === option}
            onClick={() => setPreference(option)}
            className={TOGGLE_CLASS}
          >
            {t(THEME_KEY[option])}
          </button>
        ))}
      </div>
    </main>
  )
}
