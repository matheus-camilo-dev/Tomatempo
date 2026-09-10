/**
 * A parte pura da fila offline (§3.4): o que um ciclo cumprido sem servidor
 * vira, e em que tamanho ele sobe.
 *
 * Sem IndexedDB e sem rede — quem guarda e quem envia é `lib/offlineQueue`.
 * Aqui ficam as regras, que são as mesmas do ciclo online: o parcial mínimo de
 * 5 minutos e o teto de 25 (REGRAS §1).
 */

import { creditedFocusMinutes } from './timer'

/**
 * Teto de itens por lote. Espelha `RuleConstants.MaxOfflineBatchItems` (§3.4):
 * lote maior é recusado inteiro pelo servidor, com `invalid_request`, então
 * quem parte a fila é o cliente.
 */
export const MAX_OFFLINE_BATCH_ITEMS = 100

/** Um ciclo esperando a rede voltar. É esta a forma que vai no lote (§4). */
export interface QueuedSession {
  /** Idempotência do lote: reenviar o mesmo id é no-op no servidor (§3.4). */
  readonly clientSessionId: string
  /** Instante absoluto do início, ISO 8601. O dia local quem resolve é o servidor. */
  readonly startedAt: string
  readonly durationSeconds: number
}

export interface CompletedOffline {
  readonly clientSessionId: string
  /** Epoch ms local do início do ciclo. */
  readonly startedAt: number
  readonly elapsedMs: number
}

/**
 * O que o ciclo cumprido sem servidor leva para a fila; `null` quando não leva
 * nada.
 *
 * Trunca pela mesma regra do parcial (§3.1) antes de enfileirar: parcial abaixo
 * de 5 minutos o servidor recusaria de qualquer forma, e mandá-lo só gastaria
 * a viagem. O teto de 25 minutos é o que impede um relógio adiantado durante o
 * ciclo de virar sessão maior que o próprio pomodoro (§7.3.3).
 */
export function queuedSession(cycle: CompletedOffline): QueuedSession | null {
  const minutos = creditedFocusMinutes(cycle.elapsedMs)
  if (minutos === 0) return null

  return {
    clientSessionId: cycle.clientSessionId,
    startedAt: new Date(cycle.startedAt).toISOString(),
    durationSeconds: minutos * 60,
  }
}

/** Parte a fila em lotes que o servidor aceita, na ordem em que foram cumpridos. */
export function batches<T>(
  items: readonly T[],
  size: number = MAX_OFFLINE_BATCH_ITEMS,
): T[][] {
  const lotes: T[][] = []

  for (let inicio = 0; inicio < items.length; inicio += size) {
    lotes.push(items.slice(inicio, inicio + size))
  }

  return lotes
}
