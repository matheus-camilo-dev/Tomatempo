import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { queuedSession } from '../domain/offline'
import { CYCLE_DURATION_MS, HEARTBEAT_INTERVAL_MS } from '../domain/timer'
import { ApiError, apiFetch } from '../lib/api'
import { enqueueSession } from '../lib/offlineQueue'
import { TODAY_QUERY_KEY } from '../lib/queryClient'
import { toLocalWindow, type LocalWindow, type StartedSession, type TodayStats } from '../lib/serverSession'
import { useAccount } from './useAccount'
import { useSessionHint } from './useSessionHint'
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
  /** `code` do motivo, quando o servidor tirou a sessão desta aba; senão `null`. */
  readonly lostSession: string | null
}

/**
 * Motivos que tiram a sessão desta aba para sempre (§3.3, §3.2). Insistir neles
 * é bater numa sessão que não é mais nossa — e a de verdade está em outra aba.
 */
const PERDAS = ['session_superseded', 'session_expired', 'session_closed']

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

  /**
   * Há conta neste navegador — a pista, e não `GET /me` (§7.2/A01). É ela que
   * responde offline, que é justamente quando a fila tem o que recolher: quem
   * abriu o app sem rede não tem conta carregada, e mesmo assim o ciclo dele é
   * de uma conta (§3.4).
   */
  const temConta = useSessionHint()

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

  /**
   * Instante em que começou o ciclo que **não** conseguiu abrir sessão. É o que
   * distingue o ciclo que vai para a fila do ciclo que o servidor já conhece —
   * esse tem varredor para encerrá-lo, e enfileirá-lo o contaria duas vezes
   * (§3.2, §3.4).
   */
  const semServidor = useRef<number | null>(null)

  const [perda, setPerda] = useState<string | null>(null)

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

  /**
   * Prova de presença (§3.2). Falha de rede não é tratada: a batida seguinte
   * reata, e é para isso que o servidor tolera 90s de buraco. Já o 409 é
   * definitivo — a sessão saiu desta aba e o ciclo daqui para de pontuar.
   */
  const bater = useCallback(async () => {
    const id = sessao.current
    if (id === null) return

    try {
      await apiFetch(`/sessions/${id}/beat`, { method: 'POST' })
    } catch (erro) {
      if (!(erro instanceof ApiError) || !PERDAS.includes(erro.code)) return

      sessao.current = null
      setPerda(erro.code)

      // Para sem creditar: o parcial deste ciclo já foi apurado por quem
      // assumiu, e creditar de novo aqui o contaria duas vezes (§3.3).
      store.getState().stop()
      await releerODia()
    }
  }, [store, releerODia])

  useEffect(() => {
    const intervalo = setInterval(() => void bater(), HEARTBEAT_INTERVAL_MS)

    // O navegador estrangula o intervalo na aba em segundo plano, e a cadeia
    // arrebentaria por isso. Bater na volta é o que a mantém inteira.
    const naVolta = () => void bater()
    document.addEventListener('visibilitychange', naVolta)

    return () => {
      clearInterval(intervalo)
      document.removeEventListener('visibilitychange', naVolta)
    }
  }, [bater])

  const openFocus = useCallback(async (): Promise<LocalWindow | null> => {
    // Pausa não tem sessão no servidor: `focus_sessions` só guarda foco (§2).
    if (!temConta || store.getState().timer.kind !== 'focus') return null

    // Ciclo novo, aviso velho fora da tela.
    setPerda(null)

    const inicio = store.getState().timer.startedAt

    try {
      const aberta = await apiFetch<StartedSession>('/sessions/start', {
        method: 'POST',
        json: { kind: 'focus' },
      })

      sessao.current = aberta.sessionId
      semServidor.current = null

      return toLocalWindow(aberta, Date.now())
    } catch {
      // Offline é estado normal deste produto: o ciclo começa local e o que foi
      // cumprido sobe depois pelo lote (§3.4).
      semServidor.current = inicio

      return null
    }
  }, [temConta, store])

  /**
   * Manda para a fila o ciclo que correu sem servidor. Parcial abaixo de 5
   * minutos não entra: é a mesma regra do crédito (§3.1), aplicada antes de
   * gastar a viagem que o servidor recusaria.
   */
  const enfileirar = useCallback(async (inicio: number, elapsedMs: number) => {
    semServidor.current = null

    const item = queuedSession({
      clientSessionId: crypto.randomUUID(),
      startedAt: inicio,
      elapsedMs,
    })

    if (item) await enqueueSession(item)
  }, [])

  const abandonFocus = useCallback(() => {
    const inicio = semServidor.current
    if (inicio !== null) return void enfileirar(inicio, Date.now() - inicio)

    void fechar('abandon')
  }, [enfileirar, fechar])

  useEffect(
    () =>
      store.subscribe((state, previous) => {
        const acabou =
          previous.timer.status === 'running' &&
          state.timer.status === 'finished' &&
          state.timer.kind === 'focus'

        if (!acabou) return

        // Sem sessão no servidor, o ciclo inteiro vai para a fila; com ela, o
        // fim é o `complete`, e um exclui o outro.
        const inicio = semServidor.current
        if (inicio !== null) void enfileirar(inicio, CYCLE_DURATION_MS.focus)
        else void fechar('complete')
      }),
    [store, fechar, enfileirar],
  )

  return useMemo(
    () => ({
      signedIn: comConta,
      today: ativo ? (hoje.data ?? null) : null,
      openFocus,
      abandonFocus,
      lostSession: perda,
    }),
    [comConta, ativo, hoje.data, openFocus, abandonFocus, perda],
  )
}
