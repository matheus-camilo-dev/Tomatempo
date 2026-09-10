import { describe, expect, it } from 'vitest'
import { toLocalWindow, type StartedSession } from './serverSession'

const MINUTE = 60_000

/** Sessão recém-aberta: o servidor carimba início e fim, e diz que horas são lá. */
function abertaAs(serverNow: string, startedAt = serverNow): StartedSession {
  return {
    sessionId: '0199b0e0-0000-7000-8000-000000000000',
    startedAt,
    endsAt: new Date(Date.parse(startedAt) + 25 * MINUTE).toISOString(),
    serverNow,
  }
}

describe('janela da sessão no relógio desta máquina', () => {
  it('deve manter os instantes, quando os dois relógios batem', () => {
    const sessao = abertaAs('2026-09-03T12:00:00.000Z')

    const janela = toLocalWindow(sessao, Date.parse('2026-09-03T12:00:00.000Z'))

    expect(janela.startedAt).toBe(Date.parse('2026-09-03T12:00:00.000Z'))
    expect(janela.endsAt).toBe(Date.parse('2026-09-03T12:25:00.000Z'))
  })

  // O caso que a regra existe para cobrir: adiantar o relógio da máquina não
  // pode encurtar nem esticar o ciclo (§3.2).
  it('deve descontar o adiantamento da máquina', () => {
    const sessao = abertaAs('2026-09-03T12:00:00.000Z')
    const agoraLocal = Date.parse('2026-09-03T12:10:00.000Z')

    const janela = toLocalWindow(sessao, agoraLocal)

    expect(janela.endsAt - agoraLocal).toBe(25 * MINUTE)
  })

  it('deve descontar o atraso da máquina', () => {
    const sessao = abertaAs('2026-09-03T12:00:00.000Z')
    const agoraLocal = Date.parse('2026-09-03T11:50:00.000Z')

    const janela = toLocalWindow(sessao, agoraLocal)

    expect(janela.endsAt - agoraLocal).toBe(25 * MINUTE)
  })

  // Recuperação depois de recarregar: sobra o que falta, não o ciclo inteiro.
  it('deve devolver só o que falta, quando a sessão já estava em curso', () => {
    const sessao = abertaAs('2026-09-03T12:10:00.000Z', '2026-09-03T12:00:00.000Z')
    const agoraLocal = Date.parse('2026-09-03T12:10:00.000Z')

    const janela = toLocalWindow(sessao, agoraLocal)

    expect(janela.endsAt - agoraLocal).toBe(15 * MINUTE)
    expect(janela.startedAt).toBe(agoraLocal - 10 * MINUTE)
  })
})
