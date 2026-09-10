import { useSyncExternalStore } from 'react'
import { hasSessionHint, subscribeSessionHint } from '../lib/sessionHint'

/**
 * "Há conta neste navegador?" — a pista, não a verdade (§7.2/A01).
 *
 * Quem decide de quem é a requisição é sempre o cookie. Esta é a resposta que
 * existe antes de `GET /me` voltar e continua existindo quando ele não volta,
 * que é o caso de quem está offline.
 */
export function useSessionHint(): boolean {
  return useSyncExternalStore(
    subscribeSessionHint,
    () => hasSessionHint(),
    // No servidor não há armazenamento; o app nasce anônimo e o cliente corrige.
    () => false,
  )
}
