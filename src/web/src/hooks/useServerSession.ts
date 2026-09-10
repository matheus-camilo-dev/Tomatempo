import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useRef } from 'react'
import { apiFetch } from '../lib/api'
import { TODAY_QUERY_KEY } from '../lib/queryClient'
import { toLocalWindow, type LocalWindow, type StartedSession, type TodayStats } from '../lib/serverSession'
import { useAccount } from './useAccount'
import type { TimerStore } from './useTimerStore'

export interface UseServerSessionResult {
  /**
   * Se há conta em jogo — inclusive enquanto `GET /me` ainda está voltando.
   * É por ele que a tela decide de qual placar ler, e usar a pista de sessão em
   * vez de esperar a conta é o que evita o dia do anônimo piscar para quem está
   * logado (REGRAS §2).
   */
  readonly signedIn: boolean
  /** O dia apurado pelo servidor; `null` enquanto não chega e para o anônimo. */
  readonly today: TodayStats | null
  /** Abre a sessão de foco. Devolve a janela no relógio local, ou `null`. */
  openFocus(): Promise<LocalWindow | null>
  /** Encerra a sessão aberta por abandono. Sem sessão aberta, é no-op. */
  abandonFocus(): void
}

/**
 * A ponte entre o timer da tela e a sessão validada no servidor (§3.2).
 *
 * O ciclo local nunca depende dela: quem está sem conta não abre sessão alguma,
 * e quem está com a rede fora segue contando igual. É por isso que toda chamada
 * daqui é best-effort — a conversa com o servidor rende minuto de ranking, não
 * autoriza o ciclo a começar (§3.4, §5).
 *
 * A conclusão não é clique de ninguém: ela acontece quando o relógio vira, e
 * por isso é observada na transição do store, do mesmo jeito que o alarme.
 */
export function useServerSession(store: TimerStore): UseServerSessionResult {
  const { account, isLoading } = useAccount()
  const client = useQueryClient()

  // Enquanto `GET /me` não responde, `account` é nulo e nada é perguntado — é o
  // que mantém o visitante anônimo sem uma única chamada (§5).
  const ativo = account !== null

  // `isLoading` só é verdadeiro quando existe pista de sessão neste navegador,
  // então ele é a resposta a "vem conta aí?" antes de a conta chegar.
  const comConta = ativo || isLoading

  const hoje = useQuery({
    queryKey: TODAY_QUERY_KEY,
    queryFn: () => apiFetch<TodayStats>('/stats/today'),
    enabled: ativo,
  })

  /**
   * A sessão aberta agora. É `ref` e não estado porque nada na tela é desenhado
   * a partir dela: quem a lê são os fechamentos, e um re-render por id novo
   * seria só ruído.
   */
  const sessao = useRef<string | null>(null)

  const releerODia = useCallback(
    () => client.invalidateQueries({ queryKey: TODAY_QUERY_KEY }),
    [client],
  )

  const fechar = useCallback(
    async (desfecho: 'complete' | 'abandon') => {
      const id = sessao.current
      if (id === null) return

      // Limpa antes de esperar: a resposta pode demorar, e o próximo ciclo não
      // pode encontrar o id do anterior aqui.
      sessao.current = null

      try {
        await apiFetch(`/sessions/${id}/${desfecho}`, { method: 'POST' })
      } catch {
        // Fechar é o que o servidor precisa saber, não o que a tela precisa
        // esperar: sem resposta, o varredor encerra a sessão sozinho (§3.2).
      }

      await releerODia()
    },
    [releerODia],
  )

  const openFocus = useCallback(async (): Promise<LocalWindow | null> => {
    // Pausa não tem sessão no servidor: `focus_sessions` só guarda foco (§2).
    if (!ativo || store.getState().timer.kind !== 'focus') return null

    try {
      const aberta = await apiFetch<StartedSession>('/sessions/start', {
        method: 'POST',
        json: { kind: 'focus' },
      })

      sessao.current = aberta.sessionId

      return toLocalWindow(aberta, Date.now())
    } catch {
      // Offline é estado normal deste produto: o ciclo começa local e o que foi
      // cumprido sobe depois pelo lote (§3.4).
      return null
    }
  }, [ativo, store])

  const abandonFocus = useCallback(() => void fechar('abandon'), [fechar])

  useEffect(
    () =>
      store.subscribe((state, previous) => {
        const acabou =
          previous.timer.status === 'running' &&
          state.timer.status === 'finished' &&
          state.timer.kind === 'focus'

        if (acabou) void fechar('complete')
      }),
    [store, fechar],
  )

  return useMemo(
    () => ({
      signedIn: comConta,
      today: ativo ? (hoje.data ?? null) : null,
      openFocus,
      abandonFocus,
    }),
    [comConta, ativo, hoje.data, openFocus, abandonFocus],
  )
}
