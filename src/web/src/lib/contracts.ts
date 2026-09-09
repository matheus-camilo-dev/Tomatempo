/**
 * Espelho dos contratos de saída da API (§4). São os campos que o servidor
 * projeta explicitamente — a entidade inteira nunca vem, então o tipo aqui é a
 * lista fechada do que existe, não um `Record` aberto.
 */

export interface GoalView {
  readonly current: number
  readonly upcoming: number | null
  /** Dia local em que a meta editada passa a valer, no formato `AAAA-MM-DD`. */
  readonly upcomingEffectiveFrom: string | null
}

export interface StreakView {
  readonly current: number
  readonly longest: number
}

export interface Account {
  readonly email: string | null
  readonly emailConfirmed: boolean
  /** Decide se a exclusão pede senha; conta criada pelo Google não tem uma (§3.10). */
  readonly hasPassword: boolean
  readonly nickname: string | null
  readonly onboardingRequired: boolean
  readonly timeZone: string
  readonly locale: string
  readonly colorMode: string
  readonly themePresetId: string | null
  readonly avatarId: string | null
  readonly rankingOptOut: boolean
  /** Nulo enquanto o onboarding não terminou: ainda não houve meta escolhida. */
  readonly goal: GoalView | null
  readonly streak: StreakView
  readonly freezes: { readonly balance: number }
}

export interface FreezeEntry {
  readonly delta: number
  readonly reason: string
  readonly refDate: string | null
  readonly createdAt: string
}

export interface FreezeLedger {
  readonly balance: number
  readonly entries: readonly FreezeEntry[]
}

export interface GoalChange {
  readonly pomodoros: number
  readonly effectiveFrom: string
}
