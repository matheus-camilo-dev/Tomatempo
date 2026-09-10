/**
 * Núcleo do ciclo pomodoro — puro, sem React, sem relógio ambiente.
 *
 * Todas as funções recebem o instante (`now`, epoch em ms) como parâmetro. É o
 * que permite testar virada de ciclo, aba em segundo plano e abandono sem
 * `sleep` e sem depender do relógio da máquina (§6.4).
 *
 * Regras implementadas aqui: docs/REGRAS-DE-NEGOCIO.md §1.
 */

export type CycleKind = 'focus' | 'shortBreak' | 'longBreak'

export type TimerStatus =
  /** Ciclo definido, aguardando o usuário iniciar. */
  | 'idle'
  /** Em contagem. */
  | 'running'
  /** Chegou ao fim e aguarda confirmação do usuário — nunca emenda sozinho. */
  | 'finished'

export interface TimerState {
  readonly kind: CycleKind
  readonly status: TimerStatus
  /** Epoch ms do início; `null` fora de `running`. */
  readonly startedAt: number | null
  /** Epoch ms do fim previsto; `null` fora de `running`. */
  readonly endsAt: number | null
  /** Pomodoros concluídos no dia local — governa a pausa longa. */
  readonly completedPomodoros: number
}

export interface AbandonResult {
  readonly state: TimerState
  /** Minutos de foco creditados; 0 quando descartado pela regra do parcial. */
  readonly creditedMinutes: number
}

const MINUTE_MS = 60_000

/**
 * Durações fixas para todos os usuários. REGRAS-DE-NEGOCIO §1: nunca
 * configuráveis — alterar estes valores é mudança de regra de negócio e exige
 * o rito da §8 da especificação técnica.
 */
export const CYCLE_DURATION_MS: Readonly<Record<CycleKind, number>> = {
  focus: 25 * MINUTE_MS,
  shortBreak: 5 * MINUTE_MS,
  longBreak: 15 * MINUTE_MS,
}

/** Pausa longa a cada 4 pomodoros concluídos. REGRAS-DE-NEGOCIO §1. */
export const POMODOROS_PER_LONG_BREAK = 4

/** Parcial abaixo disso é descartado inteiro. REGRAS-DE-NEGOCIO §1. */
export const MIN_PARTIAL_MINUTES = 5

export function createIdleState(completedPomodoros = 0): TimerState {
  return {
    kind: 'focus',
    status: 'idle',
    startedAt: null,
    endsAt: null,
    completedPomodoros,
  }
}

/** Inicia o ciclo já definido. Exige estado ocioso: ninguém emenda por engano. */
export function start(state: TimerState, now: number): TimerState {
  if (state.status !== 'idle') {
    throw new Error(`Não é possível iniciar um ciclo com status "${state.status}".`)
  }

  return {
    ...state,
    status: 'running',
    startedAt: now,
    endsAt: now + CYCLE_DURATION_MS[state.kind],
  }
}

/**
 * Passa o ciclo em andamento a contar pela janela que o servidor carimbou (§3.2).
 *
 * O ciclo começa no clique, pelo relógio local, e só depois a resposta do
 * servidor chega — esperar por ela deixaria o contador parado durante a ida e
 * volta da rede. A correção é pequena por construção: as duas janelas diferem
 * pela latência, não por minutos.
 *
 * `expectedStartedAt` é a identidade do ciclo que pediu a sessão. Sem ela, uma
 * resposta atrasada cairia sobre o ciclo seguinte — quem abandona e recomeça
 * dentro da ida e volta contaria o segundo ciclo pela janela do primeiro.
 */
export function adoptWindow(
  state: TimerState,
  window: { readonly startedAt: number; readonly endsAt: number },
  expectedStartedAt: number | null,
): TimerState {
  if (state.status !== 'running' || state.startedAt !== expectedStartedAt) return state

  return { ...state, startedAt: window.startedAt, endsAt: window.endsAt }
}

/**
 * Tempo restante, sempre derivado dos instantes absolutos — nunca de um
 * contador decrementado. É o que mantém a precisão com a aba congelada em
 * segundo plano ou a máquina suspensa.
 */
export function remainingMs(state: TimerState, now: number): number {
  if (state.status !== 'running' || state.endsAt === null) {
    return CYCLE_DURATION_MS[state.kind]
  }

  return Math.max(0, state.endsAt - now)
}

/**
 * Avança o estado até o instante `now`.
 *
 * Ao atingir o fim, o ciclo apenas encerra: **não inicia o próximo**. Chamadas
 * seguintes são idempotentes, por mais tempo que tenha passado. É a trava
 * anti-idle-farm da REGRAS-DE-NEGOCIO §1.
 */
export function tick(state: TimerState, now: number): TimerState {
  if (state.status !== 'running' || state.endsAt === null) return state
  if (now < state.endsAt) return state

  return {
    ...state,
    status: 'finished',
    completedPomodoros:
      state.kind === 'focus' ? state.completedPomodoros + 1 : state.completedPomodoros,
  }
}

/** Qual ciclo vem depois do que acabou de encerrar. */
export function nextKind(state: TimerState): CycleKind {
  if (state.kind !== 'focus') return 'focus'

  return state.completedPomodoros % POMODOROS_PER_LONG_BREAK === 0 ? 'longBreak' : 'shortBreak'
}

/**
 * Confirmação de presença: o usuário reconhece o fim do ciclo e o próximo fica
 * pronto, porém **parado**. Iniciar continua sendo um ato deliberado.
 */
export function acknowledge(state: TimerState): TimerState {
  if (state.status !== 'finished') {
    throw new Error(`Só é possível confirmar um ciclo encerrado; status atual: "${state.status}".`)
  }

  return {
    kind: nextKind(state),
    status: 'idle',
    startedAt: null,
    endsAt: null,
    completedPomodoros: state.completedPomodoros,
  }
}

/**
 * Interrompe o ciclo em andamento.
 *
 * Foco abandonado credita o tempo cumprido (minutos inteiros, teto na duração
 * do ciclo) e é descartado por completo abaixo de 5 minutos. Pausa abandonada
 * não credita nada. Em nenhum caso o pomodoro conta como concluído.
 */
export function abandon(state: TimerState, now: number): AbandonResult {
  if (state.status !== 'running' || state.startedAt === null) {
    throw new Error(`Não há ciclo em andamento para abandonar; status: "${state.status}".`)
  }

  return {
    state: createIdleState(state.completedPomodoros),
    creditedMinutes:
      state.kind === 'focus' ? creditedMinutes(now - state.startedAt, state.kind) : 0,
  }
}

function creditedMinutes(elapsedMs: number, kind: CycleKind): number {
  const cappedMs = Math.min(Math.max(0, elapsedMs), CYCLE_DURATION_MS[kind])
  const minutes = Math.floor(cappedMs / MINUTE_MS)

  return minutes < MIN_PARTIAL_MINUTES ? 0 : minutes
}
