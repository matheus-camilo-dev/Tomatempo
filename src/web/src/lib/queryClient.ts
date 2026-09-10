import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query'
import { ApiError } from './api'
import { clearOfflineQueue } from './offlineQueue'
import { forgetSession } from './sessionHint'

export const ME_QUERY_KEY = ['me'] as const
export const FREEZES_QUERY_KEY = ['me', 'freezes'] as const
export const TODAY_QUERY_KEY = ['stats', 'today'] as const

/**
 * Um cliente por app — e um por teste, para que o cache de um caso não vire a
 * conta carregada do caso seguinte.
 *
 * A sessão local cai no `unauthenticated`, e não no 401: `DELETE /me` com senha
 * errada também responde 401, com `invalid_credentials`, e errar a senha não
 * pode deslogar ninguém (§5).
 */
export function createQueryClient(): QueryClient {
  const client: QueryClient = new QueryClient({
    defaultOptions: {
      queries: {
        // Erro da API é resposta, não falha de rede a insistir: repetir só
        // multiplicaria 401 e 429.
        retry: false,
        refetchOnWindowFocus: false,
      },
      mutations: { retry: false },
    },
    queryCache: new QueryCache({ onError: (erro) => derrubarSessao(client, erro) }),
    mutationCache: new MutationCache({ onError: (erro) => derrubarSessao(client, erro) }),
  })

  return client
}

function derrubarSessao(client: QueryClient, erro: unknown): void {
  if (!(erro instanceof ApiError) || erro.code !== 'unauthenticated') return

  forgetSession()
  // Cookie vencido ou revogado deixa o navegador no mesmo estado do logout, e a
  // fila offline não sabe de quem ela é: ficar para trás significaria drenar na
  // próxima conta que entrasse aqui (REGRAS §8, decisão de 10/09/2026).
  void clearOfflineQueue()
  client.setQueryData(ME_QUERY_KEY, null)
}
