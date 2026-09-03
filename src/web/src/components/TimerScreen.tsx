import type { CycleKind } from '../domain/timer'
import { nextKind } from '../domain/timer'
import { useTimer } from '../hooks/useTimer'
import type { TimerStore } from '../hooks/useTimerStore'
import { formatDuration } from '../lib/formatDuration'

const CYCLE_LABEL: Record<CycleKind, string> = {
  focus: 'Foco',
  shortBreak: 'Pausa curta',
  longBreak: 'Pausa longa',
}

const START_LABEL: Record<CycleKind, string> = {
  focus: 'Iniciar foco',
  shortBreak: 'Iniciar pausa curta',
  longBreak: 'Iniciar pausa longa',
}

const FINISHED_LABEL: Record<CycleKind, string> = {
  focus: 'Pomodoro concluído',
  shortBreak: 'Pausa concluída',
  longBreak: 'Pausa concluída',
}

export interface TimerScreenProps {
  /** Injetado nos testes; em produção usa o store padrão da aplicação. */
  readonly store?: TimerStore
}

export function TimerScreen({ store }: TimerScreenProps) {
  const { timer, remainingMs, todayTotals, start, abandon, acknowledge } = useTimer(store)

  const isRunning = timer.status === 'running'
  const isFinished = timer.status === 'finished'
  // Ciclo encerrado mostra zero: `remainingMs` volta à duração cheia fora do
  // estado corrente, o que serve ao ocioso mas não a quem acabou de terminar.
  const displayMs = isFinished ? 0 : remainingMs
  const upcoming = isFinished ? nextKind(timer) : timer.kind

  function startNext() {
    acknowledge()
    start()
  }

  return (
    <main className="mx-auto flex min-h-screen max-w-md flex-col items-center justify-center gap-8 px-6 py-12 text-center">
      <h1 className="text-xl font-semibold tracking-tight text-slate-500 dark:text-slate-400">
        Tomatempo
      </h1>

      <div className="flex flex-col items-center gap-3">
        <p
          role="status"
          aria-live="polite"
          className="text-sm font-medium uppercase tracking-widest text-slate-500 dark:text-slate-400"
        >
          {isFinished ? FINISHED_LABEL[timer.kind] : CYCLE_LABEL[timer.kind]}
        </p>

        {/*
          Sem `aria-live` aqui de propósito: um contador que fala a cada 250ms
          torna o leitor de tela inutilizável. Quem anuncia é a região acima,
          que muda só na virada de ciclo.
        */}
        <div
          role="timer"
          aria-label="Tempo restante"
          className="font-mono text-7xl font-bold tabular-nums text-slate-900 dark:text-slate-50"
        >
          {formatDuration(displayMs)}
        </div>

        <p className="text-sm text-slate-500 dark:text-slate-400">
          {timer.completedPomodoros % 4} de 4 até a pausa longa
        </p>
      </div>

      {isRunning ? (
        <button
          type="button"
          onClick={abandon}
          className="rounded-full border border-slate-300 px-8 py-3 text-base font-medium text-slate-700 transition hover:bg-slate-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-slate-900 dark:border-slate-600 dark:text-slate-200 dark:hover:bg-slate-800"
        >
          Abandonar
        </button>
      ) : (
        <button
          type="button"
          onClick={isFinished ? startNext : start}
          className="rounded-full bg-slate-900 px-8 py-3 text-base font-medium text-white transition hover:bg-slate-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-slate-900 dark:bg-slate-50 dark:text-slate-900 dark:hover:bg-slate-200"
        >
          {START_LABEL[upcoming]}
        </button>
      )}

      <p
        data-testid="today-summary"
        className="text-sm text-slate-600 dark:text-slate-300"
      >
        {todayTotals.completedPomodoros}{' '}
        {todayTotals.completedPomodoros === 1 ? 'pomodoro' : 'pomodoros'} ·{' '}
        {todayTotals.focusMinutes} min de foco hoje
      </p>

      <p
        data-testid="signup-cta"
        className="max-w-xs text-sm leading-relaxed text-slate-500 dark:text-slate-400"
      >
        Crie uma conta para acompanhar sua sequência de dias e disputar o ranking semanal.
      </p>
    </main>
  )
}
