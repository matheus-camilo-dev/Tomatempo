import { describe, expect, it } from 'vitest'
import { credit, emptyLog, localDateKey, totalsFor } from './dailyLog'

describe('localDateKey', () => {
  it('deve formatar como AAAA-MM-DD com zero à esquerda', () => {
    // 5 de março de 2026, 14:30 no fuso local
    const key = localDateKey(new Date(2026, 2, 5, 14, 30).getTime())

    expect(key).toBe('2026-03-05')
  })

  it('deve usar o dia local, não o UTC (REGRAS-DE-NEGOCIO §3)', () => {
    // 23:30 local: em fusos negativos isso já é o dia seguinte em UTC.
    const lateNight = new Date(2026, 8, 2, 23, 30)

    expect(localDateKey(lateNight.getTime())).toBe('2026-09-02')
  })

  it('deve virar o dia à meia-noite local', () => {
    expect(localDateKey(new Date(2026, 8, 2, 23, 59, 59).getTime())).toBe('2026-09-02')
    expect(localDateKey(new Date(2026, 8, 3, 0, 0, 0).getTime())).toBe('2026-09-03')
  })
})

describe('emptyLog', () => {
  it('deve começar zerado na data informada', () => {
    const log = emptyLog('2026-09-02')

    expect(log).toEqual({ date: '2026-09-02', focusMinutes: 0, completedPomodoros: 0 })
  })
})

describe('credit', () => {
  const log = emptyLog('2026-09-02')

  it('deve somar minutos e contar o pomodoro concluído', () => {
    const result = credit(log, { dateKey: '2026-09-02', minutes: 25, completed: true })

    expect(result.focusMinutes).toBe(25)
    expect(result.completedPomodoros).toBe(1)
  })

  it('deve somar minutos sem contar pomodoro quando for parcial', () => {
    const result = credit(log, { dateKey: '2026-09-02', minutes: 20, completed: false })

    expect(result.focusMinutes).toBe(20)
    expect(result.completedPomodoros).toBe(0)
  })

  it('deve acumular créditos do mesmo dia', () => {
    const first = credit(log, { dateKey: '2026-09-02', minutes: 25, completed: true })
    const second = credit(first, { dateKey: '2026-09-02', minutes: 12, completed: false })

    expect(second.focusMinutes).toBe(37)
    expect(second.completedPomodoros).toBe(1)
  })

  it('deve ignorar crédito de zero minuto (parcial descartado)', () => {
    const result = credit(log, { dateKey: '2026-09-02', minutes: 0, completed: false })

    expect(result).toEqual(log)
  })

  it('deve recusar minutos negativos', () => {
    expect(() => credit(log, { dateKey: '2026-09-02', minutes: -5, completed: false })).toThrow()
  })

  it('deve começar um dia novo quando a data muda', () => {
    const day1 = credit(log, { dateKey: '2026-09-02', minutes: 25, completed: true })
    const day2 = credit(day1, { dateKey: '2026-09-03', minutes: 25, completed: true })

    expect(day2.date).toBe('2026-09-03')
    expect(day2.focusMinutes).toBe(25)
    expect(day2.completedPomodoros).toBe(1)
  })

  it('deve creditar no dia em que o ciclo começou, não no dia em que terminou', () => {
    // Pomodoro iniciado às 23:50 e concluído às 00:15: o crédito é do dia 02.
    const crossingMidnight = credit(log, {
      dateKey: '2026-09-02',
      minutes: 25,
      completed: true,
    })

    expect(crossingMidnight.date).toBe('2026-09-02')
    expect(crossingMidnight.completedPomodoros).toBe(1)
  })
})

describe('totalsFor', () => {
  const log = credit(emptyLog('2026-09-02'), {
    dateKey: '2026-09-02',
    minutes: 50,
    completed: true,
  })

  it('deve devolver os totais quando o log é do dia consultado', () => {
    expect(totalsFor(log, '2026-09-02')).toEqual({ focusMinutes: 50, completedPomodoros: 1 })
  })

  it('deve devolver zeros quando o log é de outro dia', () => {
    // O dia virou sem novo crédito: o painel mostra o dia novo zerado,
    // sem precisar de rotina de virada rodando em segundo plano.
    expect(totalsFor(log, '2026-09-03')).toEqual({ focusMinutes: 0, completedPomodoros: 0 })
  })
})
