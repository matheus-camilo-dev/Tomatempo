/**
 * Contratos e aritmética da sessão de foco validada no servidor (§3.2, §3.3).
 *
 * A conta e o perfil moram em `contracts.ts`; aqui fica a superfície do ciclo,
 * que é a única em que o front precisa reconciliar dois relógios.
 */

export interface StartedSession {
  readonly sessionId: string
  readonly startedAt: string
  readonly endsAt: string
  /** Que horas são no servidor no instante da resposta — a régua do desconto. */
  readonly serverNow: string
}

export interface CompletedSession {
  readonly countedMinutes: number
  readonly completedToday: number
  readonly goal: number
  readonly goalMet: boolean
}

export interface AbandonedSession {
  readonly countedMinutes: number
  readonly discarded: boolean
}

/** `GET /stats/today` (§4). É esta a fonte do dia de quem está logado. */
export interface TodayStats {
  readonly localDate: string
  readonly completedPomodoros: number
  readonly focusMinutes: number
  /** Subconjunto de `focusMinutes` que vale para o ranking (§3.4). */
  readonly rankingMinutes: number
  readonly goal: number
  readonly goalMet: boolean
}

/** A janela do ciclo já traduzida para o relógio desta máquina, em epoch ms. */
export interface LocalWindow {
  readonly startedAt: number
  readonly endsAt: number
}

/**
 * Traz os instantes do servidor para o relógio local.
 *
 * O contador da tela precisa de epoch local — é dele que sai `endsAt - agora` —,
 * mas quem decide quando o ciclo acaba é o servidor. Descontar o desencontro
 * entre os dois relógios é o que faz adiantar a máquina não encurtar o ciclo
 * nem esticá-lo (§3.2): o desconto move a janela junto com o erro, e a duração
 * exibida continua sendo a que o servidor carimbou.
 */
export function toLocalWindow(session: StartedSession, localNow: number): LocalWindow {
  const adiantamentoDoServidor = Date.parse(session.serverNow) - localNow

  return {
    startedAt: Date.parse(session.startedAt) - adiantamentoDoServidor,
    endsAt: Date.parse(session.endsAt) - adiantamentoDoServidor,
  }
}
