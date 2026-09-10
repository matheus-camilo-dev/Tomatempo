import { useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect } from 'react'
import { drainOfflineQueue } from '../lib/offlineQueue'
import { TODAY_QUERY_KEY } from '../lib/queryClient'
import { useSessionHint } from './useSessionHint'

/**
 * Leva a fila offline até a conta quando dá (§3.4).
 *
 * Dispara na volta da conexão e também na abertura do app: ciclo cumprido com a
 * aba fechada não pode ficar esperando um evento `online` que talvez nunca
 * chegue. Sem pista de sessão neste navegador não drena nada — a fila do
 * anônimo não existe, e a de quem saiu já foi descartada.
 *
 * Quem observa é a pista de sessão, e não `GET /me`: offline a conta não
 * responde, e é justamente aí que a fila tem o que entregar.
 */
export function useOfflineSync(): void {
  const comConta = useSessionHint()
  const client = useQueryClient()

  const drenar = useCallback(async () => {
    if (!comConta) return

    // O dia da conta muda quando o lote entra, e ele vem do servidor — somar
    // aqui seria inventar a segunda contagem que a REGRAS §2 proíbe.
    if ((await drainOfflineQueue()) > 0) {
      await client.invalidateQueries({ queryKey: TODAY_QUERY_KEY })
    }
  }, [comConta, client])

  useEffect(() => {
    // `navigator.onLine` é otimista (§3.4): serve para não tentar à toa com a
    // interface de rede caída, e quem decide de verdade é a resposta do lote.
    if (navigator.onLine) void drenar()

    const naVolta = () => void drenar()
    globalThis.addEventListener('online', naVolta)

    return () => globalThis.removeEventListener('online', naVolta)
  }, [drenar])
}
