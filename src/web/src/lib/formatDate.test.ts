import { describe, expect, it } from 'vitest'
import { formatIsoDate } from './formatDate'

describe('data da API na tela', () => {
  it('deve formatar em português', () => {
    expect(formatIsoDate('2026-09-10', 'pt-BR')).toBe('10/09/2026')
  })

  it('deve formatar em inglês', () => {
    expect(formatIsoDate('2026-09-10', 'en')).toBe('09/10/2026')
  })

  // A API manda dia local do usuário, sem hora. Interpretar como instante e
  // reimprimir no fuso da máquina adiantaria ou atrasaria o dia — e "a partir
  // de amanhã" viraria "a partir de hoje" para meio planeta.
  it('não deve deslocar o dia por causa do fuso da máquina', () => {
    expect(formatIsoDate('2026-01-01', 'pt-BR')).toBe('01/01/2026')
    expect(formatIsoDate('2026-12-31', 'pt-BR')).toBe('31/12/2026')
  })
})
