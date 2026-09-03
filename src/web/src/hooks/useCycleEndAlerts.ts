import { useEffect } from 'react'
import type { CycleKind } from '../domain/timer'
import type { AlertMessage, AlertPorts } from '../lib/alerts'
import type { TimerStore } from './useTimerStore'

const END_MESSAGE: Record<CycleKind, AlertMessage> = {
  focus: { title: 'Pomodoro concluído', body: 'Hora da pausa.' },
  shortBreak: { title: 'Pausa concluída', body: 'Hora de voltar ao foco.' },
  longBreak: { title: 'Pausa concluída', body: 'Hora de voltar ao foco.' },
}

/**
 * Avisa o usuário no fim de cada ciclo.
 *
 * Dispara na **transição** de `running` para `finished`, não no estado
 * `finished` — que persiste até o usuário confirmar (§3.1) e, se observado como
 * estado, tocaria o alarme a cada sincronização.
 *
 * A notificação só sai com a aba em segundo plano: quem está olhando a tela já
 * ouviu o alarme e vê o contador zerado, e notificar por cima disso é ruído.
 */
export function useCycleEndAlerts(store: TimerStore, alerts: AlertPorts): void {
  useEffect(
    () =>
      store.subscribe((state, previous) => {
        const justEnded = previous.timer.status === 'running' && state.timer.status === 'finished'
        if (!justEnded) return

        alerts.playAlarm()
        if (alerts.isHidden()) alerts.notify(END_MESSAGE[state.timer.kind])
      }),
    [store, alerts],
  )
}
