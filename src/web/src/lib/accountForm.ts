import { ApiError } from './api'

/** Peças de formulário de conta que não são componentes — ficam fora do módulo de UI. */

/** Menu do cadastro: as quatro metas oferecidas na entrada (§3.5). */
export const SIGNUP_GOALS = [2, 4, 6, 8] as const

/** Metas aceitas depois, quando a pessoa já conhece o próprio ritmo (§3.5). */
export const GOAL_RANGE = Array.from({ length: 24 }, (_, i) => i + 1)

/** Fuso do navegador — proposta, não imposição: a pessoa troca antes de enviar. */
export function browserTimeZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone
}

/** `null` quando não houve erro; o code do problem+json quando houve. */
export function errorCode(error: unknown): string | null {
  if (error === null || error === undefined) return null

  return error instanceof ApiError ? error.code : 'unknown'
}
