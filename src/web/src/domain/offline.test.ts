import { describe, expect, it } from 'vitest'
import { MAX_OFFLINE_BATCH_ITEMS, batches, queuedSession } from './offline'
import { CYCLE_DURATION_MS } from './timer'

/**
 * A parte pura da fila offline (§3.4): o que um ciclo cumprido sem servidor
 * vira, e em que tamanho de lote ele sobe. Nada aqui toca IndexedDB nem rede —
 * é entra valor, sai valor.
 *
 * Critérios da §5: "abandono offline entra com o parcial truncado; abaixo de 5
 * minutos não entra" e "fila com mais itens que o teto do lote sobe em lotes
 * sucessivos, nenhum acima do teto".
 */

const MINUTE = 60_000
const ID = '0199b0e0-0000-7000-8000-000000000000'
const INICIO = Date.parse('2026-09-10T12:00:00.000Z')

function paraAFila(elapsedMs: number) {
  return queuedSession({ clientSessionId: ID, startedAt: INICIO, elapsedMs })
}

describe('queuedSession', () => {
  it('deve levar o pomodoro inteiro com os 25 minutos cheios', () => {
    expect(paraAFila(CYCLE_DURATION_MS.focus)?.durationSeconds).toBe(25 * 60)
  })

  it('deve levar o instante absoluto do início', () => {
    expect(paraAFila(CYCLE_DURATION_MS.focus)?.startedAt).toBe('2026-09-10T12:00:00.000Z')
  })

  it('deve levar o clientSessionId de quem enfileirou', () => {
    expect(paraAFila(CYCLE_DURATION_MS.focus)?.clientSessionId).toBe(ID)
  })

  // O parcial é truncado em minutos inteiros antes de subir: mandar segundos
  // quebrados só daria ao servidor o trabalho de arredondar de novo (§3.1).
  it('deve truncar o parcial em minutos inteiros', () => {
    expect(paraAFila(12 * MINUTE + 43_000)?.durationSeconds).toBe(12 * 60)
  })

  it('deve levar o parcial de quem passou dos 5 minutos', () => {
    expect(paraAFila(5 * MINUTE)?.durationSeconds).toBe(5 * 60)
  })

  // Mesma regra do parcial mínimo (REGRAS §1), aplicada antes de a rede voltar:
  // enfileirar para o servidor recusar depois só gasta viagem.
  it('não deve enfileirar o parcial abaixo de 5 minutos', () => {
    expect(paraAFila(4 * MINUTE + 59_000)).toBeNull()
  })

  it('não deve enfileirar ciclo nenhum de duração zero', () => {
    expect(paraAFila(0)).toBeNull()
  })

  // Relógio da máquina adiantado durante o ciclo não pode virar sessão maior
  // que o próprio pomodoro (§7.3.3).
  it('não deve passar dos 25 minutos, por mais que o relógio ande', () => {
    expect(paraAFila(3 * 60 * MINUTE)?.durationSeconds).toBe(25 * 60)
  })
})

describe('batches', () => {
  const fila = (quantidade: number) => Array.from({ length: quantidade }, (_, i) => i)

  it('deve mandar a fila curta num lote só', () => {
    expect(batches(fila(3))).toEqual([[0, 1, 2]])
  })

  it('não deve montar lote algum para a fila vazia', () => {
    expect(batches([])).toEqual([])
  })

  it('deve partir a fila que passa do teto', () => {
    const lotes = batches(fila(MAX_OFFLINE_BATCH_ITEMS + 1))

    expect(lotes).toHaveLength(2)
    expect(lotes[0]).toHaveLength(MAX_OFFLINE_BATCH_ITEMS)
    expect(lotes[1]).toHaveLength(1)
  })

  it('deve manter todos os itens, e na ordem em que estavam', () => {
    expect(batches(fila(250)).flat()).toEqual(fila(250))
  })

  // O teto é do servidor (§3.4): lote acima dele é recusado inteiro, com
  // `invalid_request`, e não item a item.
  it('deve espelhar o teto que o servidor recusa', () => {
    expect(MAX_OFFLINE_BATCH_ITEMS).toBe(100)
  })
})
