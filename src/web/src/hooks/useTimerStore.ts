import { createStore, type StoreApi } from 'zustand/vanilla'
import {
  CYCLE_DURATION_MS,
  abandon as abandonCycle,
  acknowledge as acknowledgeCycle,
  adoptWindow as adopt,
  createIdleState,
  resumeFocus,
  remainingMs as remainingOf,
  start as startCycle,
  tick,
  type TimerState,
} from '../domain/timer'
import {
  credit,
  emptyLog,
  localDateKey,
  totalsFor,
  type DailyLog,
  type DailyTotals,
} from '../domain/dailyLog'
import type { LocalWindow } from '../lib/serverSession'

const STORAGE_KEY = 'tomatempo:timer:v1'

export interface TimerStoreState {
  readonly timer: TimerState
  readonly log: DailyLog
  /** Instante da última sincronização — base do tempo restante exibido. */
  readonly nowMs: number
  readonly remainingMs: number
  /**
   * Dia local em que o ciclo corrente começou — é nele que o crédito entra,
   * mesmo que o ciclo termine depois da meia-noite (§3.6). Sempre definido: só
   * é lido depois de um `start`, que o atualiza.
   */
  readonly runningDateKey: string
  start(): void
  /** Passa a contar pela janela do servidor, se o ciclo ainda for o mesmo. */
  adoptWindow(window: LocalWindow, expectedStartedAt: number | null): void
  /**
   * Retoma o foco que o servidor diz estar correndo, venha a tela do ocioso ou
   * de um foco já em andamento (§3.2).
   */
  resume(window: LocalWindow): void
  /**
   * Volta ao ocioso sem creditar nada. É o desfecho de quem perdeu a sessão
   * para outra aba: o parcial já foi apurado lá, e creditar de novo aqui o
   * contaria duas vezes (§3.3).
   */
  stop(): void
  sync(): void
  acknowledge(): void
  abandon(): void
}

export interface TimerStoreOptions {
  /** `null` cobre navegação privada e navegadores que bloqueiam storage. */
  readonly storage?: Storage | null
}

interface PersistedShape {
  readonly timer: TimerState
  readonly log: DailyLog
  readonly runningDateKey: string
}

export type TimerStore = StoreApi<TimerStoreState>

export function selectTodayTotals(state: TimerStoreState): DailyTotals {
  return totalsFor(state.log, localDateKey(state.nowMs))
}

export function createTimerStore(options: TimerStoreOptions = {}): TimerStore {
  const storage = options.storage === undefined ? safeLocalStorage() : options.storage
  const now = Date.now()
  const restored = restore(storage, now)

  return createStore<TimerStoreState>((set, get) => {
    /** Aplica o relógio ao estado e credita o que porventura tenha terminado. */
    function advance(state: TimerStoreState, at: number): TimerStoreState {
      const advanced = tick(state.timer, at)
      const justCompleted =
        advanced.status === 'finished' &&
        state.timer.status === 'running' &&
        advanced.kind === 'focus'

      return {
        ...state,
        timer: advanced,
        nowMs: at,
        remainingMs: remainingOf(advanced, at),
        log: justCompleted
          ? credit(state.log, {
              dateKey: state.runningDateKey,
              minutes: CYCLE_DURATION_MS.focus / 60_000,
              completed: true,
            })
          : state.log,
      }
    }

    function commit(next: TimerStoreState): void {
      persist(storage, next)
      set(next)
    }

    return {
      ...restored,

      start() {
        const at = Date.now()
        const state = get()
        const timer = startCycle(state.timer, at)

        commit({
          ...state,
          timer,
          nowMs: at,
          remainingMs: remainingOf(timer, at),
          runningDateKey: localDateKey(at),
        })
      },

      stop() {
        const at = Date.now()
        const state = get()
        const timer = createIdleState(state.timer.completedPomodoros)

        commit({ ...state, timer, nowMs: at, remainingMs: remainingOf(timer, at) })
      },

      resume(window) {
        const state = get()
        const timer = resumeFocus(state.timer, window)
        if (timer === state.timer) return

        const at = Date.now()

        commit({ ...state, timer, nowMs: at, remainingMs: remainingOf(timer, at) })
      },

      adoptWindow(window, expectedStartedAt) {
        const state = get()
        const timer = adopt(state.timer, window, expectedStartedAt)
        if (timer === state.timer) return

        const at = Date.now()

        commit({ ...state, timer, nowMs: at, remainingMs: remainingOf(timer, at) })
      },

      sync() {
        commit(advance(get(), Date.now()))
      },

      acknowledge() {
        const at = Date.now()
        const state = advance(get(), at)
        const timer = acknowledgeCycle(state.timer)

        commit({
          ...state,
          timer,
          remainingMs: remainingOf(timer, at),
        })
      },

      abandon() {
        const at = Date.now()
        const state = get()
        const { state: timer, creditedMinutes } = abandonCycle(state.timer, at)

        commit({
          ...state,
          timer,
          nowMs: at,
          remainingMs: remainingOf(timer, at),
          log: credit(state.log, {
            dateKey: state.runningDateKey,
            minutes: creditedMinutes,
            completed: false,
          }),
        })
      },
    }
  })
}

/**
 * Estado inicial: o que estava salvo, já avançado até agora. Um ciclo que
 * terminou com a aba fechada aparece encerrado e creditado na volta — o crédito
 * não depende de a página estar aberta no instante do fim.
 */
function restore(storage: Storage | null, at: number): Omit<
  TimerStoreState,
  'start' | 'sync' | 'acknowledge' | 'abandon' | 'adoptWindow' | 'resume' | 'stop'
> {
  const persisted = readPersisted(storage)
  const fresh = {
    timer: createIdleState(),
    log: emptyLog(localDateKey(at)),
    runningDateKey: localDateKey(at),
  }
  const base = persisted ?? fresh
  const advanced = tick(base.timer, at)
  const justCompleted =
    advanced.status === 'finished' && base.timer.status === 'running' && advanced.kind === 'focus'

  return {
    timer: advanced,
    nowMs: at,
    remainingMs: remainingOf(advanced, at),
    runningDateKey: base.runningDateKey,
    log: justCompleted
      ? credit(base.log, {
          dateKey: base.runningDateKey,
          minutes: CYCLE_DURATION_MS.focus / 60_000,
          completed: true,
        })
      : base.log,
  }
}

function readPersisted(storage: Storage | null): PersistedShape | null {
  if (!storage) return null

  try {
    const raw = storage.getItem(STORAGE_KEY)
    if (!raw) return null

    const parsed: unknown = JSON.parse(raw)

    return isPersisted(parsed) ? parsed : null
  } catch {
    // Dado corrompido ou storage bloqueado: o app começa limpo em vez de quebrar.
    return null
  }
}

function persist(storage: Storage | null, state: TimerStoreState): void {
  if (!storage) return

  const shape: PersistedShape = {
    timer: state.timer,
    log: state.log,
    runningDateKey: state.runningDateKey,
  }

  try {
    storage.setItem(STORAGE_KEY, JSON.stringify(shape))
  } catch {
    // Cota estourada ou storage indisponível: o timer segue funcionando em memória.
  }
}

function isPersisted(value: unknown): value is PersistedShape {
  if (typeof value !== 'object' || value === null) return false

  const candidate = value as Partial<PersistedShape>

  return (
    typeof candidate.timer === 'object' &&
    candidate.timer !== null &&
    typeof candidate.timer.kind === 'string' &&
    typeof candidate.timer.status === 'string' &&
    typeof candidate.timer.completedPomodoros === 'number' &&
    typeof candidate.log === 'object' &&
    candidate.log !== null &&
    typeof candidate.log.date === 'string' &&
    typeof candidate.log.focusMinutes === 'number' &&
    typeof candidate.runningDateKey === 'string'
  )
}

function safeLocalStorage(): Storage | null {
  try {
    return globalThis.localStorage
  } catch {
    return null
  }
}
