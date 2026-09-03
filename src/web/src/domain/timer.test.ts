import { describe, expect, it } from 'vitest'
import {
  CYCLE_DURATION_MS,
  MIN_PARTIAL_MINUTES,
  POMODOROS_PER_LONG_BREAK,
  abandon,
  acknowledge,
  createIdleState,
  nextKind,
  remainingMs,
  start,
  tick,
} from './timer'

const MINUTE = 60_000
const T0 = 1_756_000_000_000 // instante arbitrário, fixo: nada depende do relógio real

/** Estado de foco em andamento, iniciado em T0. */
const runningFocus = (completedPomodoros = 0) =>
  start(createIdleState(completedPomodoros), T0)

describe('durações do ciclo (REGRAS-DE-NEGOCIO §1)', () => {
  it('deve fixar foco em 25 minutos', () => {
    expect(CYCLE_DURATION_MS.focus).toBe(25 * MINUTE)
  })

  it('deve fixar pausa curta em 5 minutos', () => {
    expect(CYCLE_DURATION_MS.shortBreak).toBe(5 * MINUTE)
  })

  it('deve fixar pausa longa em 15 minutos', () => {
    expect(CYCLE_DURATION_MS.longBreak).toBe(15 * MINUTE)
  })

  it('deve exigir 4 pomodoros para a pausa longa', () => {
    expect(POMODOROS_PER_LONG_BREAK).toBe(4)
  })

  it('deve descartar parciais abaixo de 5 minutos', () => {
    expect(MIN_PARTIAL_MINUTES).toBe(5)
  })
})

describe('createIdleState', () => {
  it('deve começar ocioso, em foco e sem pomodoros', () => {
    const state = createIdleState()

    expect(state.status).toBe('idle')
    expect(state.kind).toBe('focus')
    expect(state.completedPomodoros).toBe(0)
    expect(state.startedAt).toBeNull()
    expect(state.endsAt).toBeNull()
  })

  it('deve preservar os pomodoros já concluídos no dia', () => {
    expect(createIdleState(3).completedPomodoros).toBe(3)
  })
})

describe('start', () => {
  it('deve marcar início e fim absolutos a partir do instante recebido', () => {
    const state = start(createIdleState(), T0)

    expect(state.status).toBe('running')
    expect(state.startedAt).toBe(T0)
    expect(state.endsAt).toBe(T0 + 25 * MINUTE)
  })

  it('deve recusar iniciar um ciclo já em andamento', () => {
    expect(() => start(runningFocus(), T0 + MINUTE)).toThrow()
  })

  it('deve recusar iniciar sobre um ciclo encerrado ainda não confirmado', () => {
    const finished = tick(runningFocus(), T0 + 25 * MINUTE)

    expect(() => start(finished, T0 + 26 * MINUTE)).toThrow()
  })
})

describe('remainingMs', () => {
  it('deve devolver a duração cheia quando ocioso', () => {
    expect(remainingMs(createIdleState(), T0)).toBe(25 * MINUTE)
  })

  it('deve descontar o tempo decorrido', () => {
    expect(remainingMs(runningFocus(), T0 + 10 * MINUTE)).toBe(15 * MINUTE)
  })

  it('deve calcular por timestamp absoluto, sobrevivendo a aba em segundo plano', () => {
    // A aba ficou congelada 20 minutos: nenhum "tick" aconteceu nesse intervalo.
    // O restante depende só dos instantes, nunca de um contador decrementado.
    expect(remainingMs(runningFocus(), T0 + 20 * MINUTE)).toBe(5 * MINUTE)
  })

  it('nunca deve devolver valor negativo', () => {
    expect(remainingMs(runningFocus(), T0 + 90 * MINUTE)).toBe(0)
  })
})

describe('tick', () => {
  it('deve manter o ciclo em andamento antes do fim', () => {
    const state = tick(runningFocus(), T0 + 24 * MINUTE + 59_000)

    expect(state.status).toBe('running')
    expect(state.completedPomodoros).toBe(0)
  })

  it('deve encerrar o foco no instante exato do fim', () => {
    const state = tick(runningFocus(), T0 + 25 * MINUTE)

    expect(state.status).toBe('finished')
  })

  it('deve contar o pomodoro concluído', () => {
    expect(tick(runningFocus(2), T0 + 25 * MINUTE).completedPomodoros).toBe(3)
  })

  it('não deve contar pomodoro ao fim de uma pausa', () => {
    const breakIdle = acknowledge(tick(runningFocus(), T0 + 25 * MINUTE))
    const breakRunning = start(breakIdle, T0 + 26 * MINUTE)

    const state = tick(breakRunning, T0 + 31 * MINUTE)

    expect(state.status).toBe('finished')
    expect(state.completedPomodoros).toBe(1)
  })

  it('NUNCA deve emendar o próximo ciclo sozinho (anti-idle-farm)', () => {
    const finished = tick(runningFocus(), T0 + 25 * MINUTE)

    // Uma hora depois, sem nenhuma interação do usuário.
    const muchLater = tick(tick(finished, T0 + 40 * MINUTE), T0 + 85 * MINUTE)

    expect(muchLater.status).toBe('finished')
    expect(muchLater.kind).toBe('focus')
    expect(muchLater.completedPomodoros).toBe(1)
  })

  it('deve ser idempotente depois de encerrado', () => {
    const finished = tick(runningFocus(), T0 + 25 * MINUTE)

    expect(tick(finished, T0 + 26 * MINUTE)).toEqual(finished)
  })

  it('deve ignorar estado ocioso', () => {
    const idle = createIdleState()

    expect(tick(idle, T0 + 99 * MINUTE)).toEqual(idle)
  })
})

describe('nextKind (REGRAS-DE-NEGOCIO §1: pausa longa a cada 4 ciclos)', () => {
  const finishedFocusAfter = (completedBefore: number) =>
    tick(runningFocus(completedBefore), T0 + 25 * MINUTE)

  it('deve indicar pausa curta após o 1º pomodoro', () => {
    expect(nextKind(finishedFocusAfter(0))).toBe('shortBreak')
  })

  it('deve indicar pausa curta após o 3º pomodoro', () => {
    expect(nextKind(finishedFocusAfter(2))).toBe('shortBreak')
  })

  it('deve indicar pausa longa após o 4º pomodoro', () => {
    expect(nextKind(finishedFocusAfter(3))).toBe('longBreak')
  })

  it('deve indicar pausa longa após o 8º pomodoro', () => {
    expect(nextKind(finishedFocusAfter(7))).toBe('longBreak')
  })

  it('deve voltar ao foco depois de qualquer pausa', () => {
    const shortBreakFinished = tick(
      start({ ...createIdleState(1), kind: 'shortBreak' }, T0),
      T0 + 5 * MINUTE,
    )
    const longBreakFinished = tick(
      start({ ...createIdleState(4), kind: 'longBreak' }, T0),
      T0 + 15 * MINUTE,
    )

    expect(nextKind(shortBreakFinished)).toBe('focus')
    expect(nextKind(longBreakFinished)).toBe('focus')
  })
})

describe('acknowledge (confirmação de presença)', () => {
  it('deve deixar o próximo ciclo pronto, porém parado', () => {
    const state = acknowledge(tick(runningFocus(), T0 + 25 * MINUTE))

    expect(state.status).toBe('idle')
    expect(state.kind).toBe('shortBreak')
    expect(state.startedAt).toBeNull()
    expect(state.endsAt).toBeNull()
  })

  it('deve preservar a contagem de pomodoros do dia', () => {
    const state = acknowledge(tick(runningFocus(3), T0 + 25 * MINUTE))

    expect(state.completedPomodoros).toBe(4)
    expect(state.kind).toBe('longBreak')
  })

  it('deve recusar confirmar um ciclo que ainda não terminou', () => {
    expect(() => acknowledge(runningFocus())).toThrow()
  })
})

describe('abandon (REGRAS-DE-NEGOCIO §1: tempo parcial conta)', () => {
  it('deve creditar 20 minutos ao abandonar no minuto 20', () => {
    expect(abandon(runningFocus(), T0 + 20 * MINUTE).creditedMinutes).toBe(20)
  })

  it('deve creditar exatamente 5 minutos no limite mínimo', () => {
    expect(abandon(runningFocus(), T0 + 5 * MINUTE).creditedMinutes).toBe(5)
  })

  it('deve descartar parcial de 4min59s', () => {
    expect(abandon(runningFocus(), T0 + 4 * MINUTE + 59_000).creditedMinutes).toBe(0)
  })

  it('deve arredondar para baixo os segundos', () => {
    expect(abandon(runningFocus(), T0 + 12 * MINUTE + 59_000).creditedMinutes).toBe(12)
  })

  it('nunca deve creditar mais que a duração do foco', () => {
    expect(abandon(runningFocus(), T0 + 90 * MINUTE).creditedMinutes).toBe(25)
  })

  it('não deve creditar minutos por pausa abandonada', () => {
    const breakRunning = start({ ...createIdleState(1), kind: 'shortBreak' }, T0)

    expect(abandon(breakRunning, T0 + 4 * MINUTE).creditedMinutes).toBe(0)
  })

  it('não deve contar o pomodoro abandonado como concluído', () => {
    const { state } = abandon(runningFocus(2), T0 + 24 * MINUTE)

    expect(state.completedPomodoros).toBe(2)
  })

  it('deve voltar para um foco parado, sem emendar nada', () => {
    const { state } = abandon(runningFocus(), T0 + 20 * MINUTE)

    expect(state.status).toBe('idle')
    expect(state.kind).toBe('focus')
    expect(state.startedAt).toBeNull()
  })

  it('deve recusar abandonar o que não está em andamento', () => {
    expect(() => abandon(createIdleState(), T0)).toThrow()
  })
})
