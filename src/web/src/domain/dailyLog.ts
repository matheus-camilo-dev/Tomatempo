/**
 * Totais de foco do dia local — puro, sem armazenamento e sem relógio ambiente.
 *
 * O "dia" segue o fuso local do usuário (docs/REGRAS-DE-NEGOCIO.md §3), nunca
 * UTC. Na fase 1 esses totais existem só no navegador; na fase 2 o servidor
 * passa a ser a fonte da verdade e este módulo continua descrevendo a mesma
 * regra do lado do cliente.
 */

export interface DailyLog {
  /** Dia local no formato AAAA-MM-DD. */
  readonly date: string
  readonly focusMinutes: number
  readonly completedPomodoros: number
}

export interface DailyTotals {
  readonly focusMinutes: number
  readonly completedPomodoros: number
}

export interface FocusCredit {
  /** Dia local em que o ciclo **começou** — é nele que o crédito entra. */
  readonly dateKey: string
  readonly minutes: number
  /** `true` só quando o pomodoro foi até o fim; parcial não conta. */
  readonly completed: boolean
}

const EMPTY_TOTALS: DailyTotals = { focusMinutes: 0, completedPomodoros: 0 }

/** Dia local de um instante, no formato AAAA-MM-DD. */
export function localDateKey(epochMs: number): string {
  const date = new Date(epochMs)
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')

  return `${date.getFullYear()}-${month}-${day}`
}

export function emptyLog(date: string): DailyLog {
  return { date, focusMinutes: 0, completedPomodoros: 0 }
}

/**
 * Soma um bloco de foco ao log.
 *
 * Crédito de outro dia começa um dia novo, em vez de acumular no antigo — é o
 * que faz a virada acontecer naturalmente, sem rotina rodando à meia-noite.
 */
export function credit(log: DailyLog, focus: FocusCredit): DailyLog {
  if (focus.minutes < 0) {
    throw new Error(`Crédito de foco não pode ser negativo: ${focus.minutes}.`)
  }

  if (focus.minutes === 0) return log

  const base = focus.dateKey === log.date ? log : emptyLog(focus.dateKey)

  return {
    date: base.date,
    focusMinutes: base.focusMinutes + focus.minutes,
    completedPomodoros: base.completedPomodoros + (focus.completed ? 1 : 0),
  }
}

/**
 * Totais de um dia específico. Log de outro dia devolve zeros: quem consulta
 * sempre recebe o dia que pediu, mesmo que o log ainda não tenha virado.
 */
export function totalsFor(log: DailyLog, dateKey: string): DailyTotals {
  if (log.date !== dateKey) return EMPTY_TOTALS

  return { focusMinutes: log.focusMinutes, completedPomodoros: log.completedPomodoros }
}
