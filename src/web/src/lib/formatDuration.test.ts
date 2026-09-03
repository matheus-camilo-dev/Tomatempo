import { describe, expect, it } from 'vitest'
import { formatDuration } from './formatDuration'

describe('formatDuration', () => {
  it('deve formatar minutos e segundos com dois dígitos', () => {
    expect(formatDuration(25 * 60_000)).toBe('25:00')
    expect(formatDuration(5 * 60_000 + 7_000)).toBe('05:07')
  })

  it('deve arredondar para cima o segundo em curso', () => {
    // Faltando 24min59,4s ainda se lê "25:00": o número só cai quando o segundo
    // realmente vira, evitando pular de 25:00 para 24:58.
    expect(formatDuration(24 * 60_000 + 59_400)).toBe('25:00')
  })

  it('deve mostrar zero ao fim', () => {
    expect(formatDuration(0)).toBe('00:00')
  })

  it('deve tratar valor negativo como zero', () => {
    expect(formatDuration(-5_000)).toBe('00:00')
  })
})
