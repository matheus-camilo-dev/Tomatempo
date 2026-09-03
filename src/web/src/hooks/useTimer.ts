import { useEffect } from 'react'
import { useStore } from 'zustand'
import type { DailyTotals } from '../domain/dailyLog'
import {
  createTimerStore,
  selectTodayTotals,
  type TimerStore,
  type TimerStoreState,
} from './useTimerStore'

/**
 * Frequência do tique. Não é a fonte da verdade do tempo — o restante vem
 * sempre de `endsAt - agora` (§3.1) —, só a frequência com que a tela relê o
 * relógio. Quatro vezes por segundo mantém os segundos visualmente exatos.
 */
export const TICK_INTERVAL_MS = 250

/** Store padrão da aplicação; os testes injetam o seu. */
export const timerStore: TimerStore = createTimerStore()

export interface UseTimerResult extends TimerStoreState {
  readonly todayTotals: DailyTotals
}

export function useTimer(store: TimerStore = timerStore): UseTimerResult {
  const state = useStore(store)

  useEffect(() => {
    const interval = setInterval(() => store.getState().sync(), TICK_INTERVAL_MS)

    // Em segundo plano o navegador estrangula o intervalo, então o relógio
    // avança muito mais do que os tiques recebidos. Ressincronizar na volta
    // corrige o estado de uma vez, inclusive encerrando um ciclo já vencido.
    const resync = () => store.getState().sync()
    document.addEventListener('visibilitychange', resync)

    return () => {
      clearInterval(interval)
      document.removeEventListener('visibilitychange', resync)
    }
  }, [store])

  return { ...state, todayTotals: selectTodayTotals(state) }
}
