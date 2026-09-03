import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import type { CycleKind } from '../domain/timer'
import type { AlertPorts } from '../lib/alerts'
import type { TimerStore } from './useTimerStore'

const MESSAGE_KEYS: Record<CycleKind, { title: string; body: string }> = {
  focus: { title: 'notification.focusEndedTitle', body: 'notification.focusEndedBody' },
  shortBreak: { title: 'notification.breakEndedTitle', body: 'notification.breakEndedBody' },
  longBreak: { title: 'notification.breakEndedTitle', body: 'notification.breakEndedBody' },
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
  const { t } = useTranslation()

  useEffect(
    () =>
      store.subscribe((state, previous) => {
        const justEnded = previous.timer.status === 'running' && state.timer.status === 'finished'
        if (!justEnded) return

        alerts.playAlarm()

        if (alerts.isHidden()) {
          const keys = MESSAGE_KEYS[state.timer.kind]
          alerts.notify({ title: t(keys.title), body: t(keys.body) })
        }
      }),
    [store, alerts, t],
  )
}
