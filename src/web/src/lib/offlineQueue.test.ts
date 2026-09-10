import 'fake-indexeddb/auto'
import { HttpResponse, http } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { MAX_OFFLINE_BATCH_ITEMS, type QueuedSession } from '../domain/offline'
import { server } from '../test/server'
import {
  clearOfflineQueue,
  drainOfflineQueue,
  enqueueSession,
  pendingSessions,
} from './offlineQueue'

/**
 * A fila offline no navegador (§3.4).
 *
 * Critérios da §5 cobertos aqui: "item sai da fila só depois da resposta;
 * recusado é descartado sem retry, aceito nunca é reenviado", "drenar duas
 * vezes a mesma fila credita uma vez só", "fila com mais itens que o teto do
 * lote sobe em lotes sucessivos", "drenagem sem resposta do servidor mantém a
 * fila inteira" e "navegador sem IndexedDB não derruba o ciclo".
 *
 * IndexedDB de mentira, API de mentira, nenhum dos dois mockado por dentro: o
 * módulo roda como roda no navegador (§6.4).
 */

const ROTA = '/api/sessions/offline-batch'

function item(numero: number): QueuedSession {
  return {
    clientSessionId: `0199b0e0-0000-7000-8000-${String(numero).padStart(12, '0')}`,
    startedAt: '2026-09-10T12:00:00.000Z',
    durationSeconds: 25 * 60,
  }
}

/** Corpo de cada lote que chegou ao servidor, na ordem em que chegou. */
function lotesRecebidos(resposta: (itens: QueuedSession[]) => HttpResponse | Response) {
  const lotes: QueuedSession[][] = []

  server.use(
    http.post(ROTA, async ({ request }) => {
      const corpo = (await request.json()) as { items: QueuedSession[] }
      lotes.push(corpo.items)

      return resposta(corpo.items)
    }),
  )

  return lotes
}

/** Servidor que aceita tudo o que chega. */
function aceitandoTudo() {
  return lotesRecebidos((itens) =>
    HttpResponse.json({
      items: itens.map((i) => ({ clientSessionId: i.clientSessionId, accepted: true, reason: null })),
    }),
  )
}

beforeEach(async () => {
  await clearOfflineQueue()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('a fila', () => {
  it('deve guardar o ciclo enfileirado', async () => {
    await enqueueSession(item(1))

    expect(await pendingSessions()).toEqual([item(1)])
  })

  it('deve guardar os ciclos na ordem em que foram cumpridos', async () => {
    await enqueueSession(item(1))
    await enqueueSession(item(2))

    expect((await pendingSessions()).map((i) => i.clientSessionId)).toEqual([
      item(1).clientSessionId,
      item(2).clientSessionId,
    ])
  })

  it('deve esvaziar quando descartada', async () => {
    await enqueueSession(item(1))

    await clearOfflineQueue()

    expect(await pendingSessions()).toEqual([])
  })
})

describe('drenagem', () => {
  it('deve mandar o que está na fila para o lote offline', async () => {
    const lotes = aceitandoTudo()
    await enqueueSession(item(1))

    await drainOfflineQueue()

    expect(lotes).toEqual([[item(1)]])
  })

  it('não deve tocar a rede com a fila vazia', async () => {
    const lotes = aceitandoTudo()

    await drainOfflineQueue()

    expect(lotes).toEqual([])
  })

  it('deve devolver quantos itens o servidor aceitou', async () => {
    lotesRecebidos((itens) =>
      HttpResponse.json({
        items: itens.map((i, indice) => ({
          clientSessionId: i.clientSessionId,
          accepted: indice === 0,
          reason: indice === 0 ? null : 'overlaps',
        })),
      }),
    )
    await enqueueSession(item(1))
    await enqueueSession(item(2))

    expect(await drainOfflineQueue()).toBe(1)
  })

  it('deve tirar da fila o item que o servidor aceitou', async () => {
    aceitandoTudo()
    await enqueueSession(item(1))

    await drainOfflineQueue()

    expect(await pendingSessions()).toEqual([])
  })

  // Recusa é veredito, não falha de entrega: insistir num item que o servidor
  // já julgou seria bater na mesma porta para sempre (§3.4).
  it('deve descartar sem retry o item que o servidor recusou', async () => {
    const lotes = lotesRecebidos((itens) =>
      HttpResponse.json({
        items: itens.map((i) => ({
          clientSessionId: i.clientSessionId,
          accepted: false,
          reason: 'too_old',
        })),
      }),
    )
    await enqueueSession(item(1))

    await drainOfflineQueue()
    await drainOfflineQueue()

    expect(await pendingSessions()).toEqual([])
    expect(lotes).toHaveLength(1)
  })

  it('não deve reenviar o que já foi aceito', async () => {
    const lotes = aceitandoTudo()
    await enqueueSession(item(1))

    await drainOfflineQueue()
    await drainOfflineQueue()

    expect(lotes).toHaveLength(1)
  })

  // O item só sai depois da resposta: sem ela, ninguém sabe se o servidor
  // creditou, e jogar fora aqui perderia minutos de verdade (§5).
  it('deve manter a fila inteira quando a resposta não vem', async () => {
    server.use(http.post(ROTA, () => HttpResponse.error()))
    await enqueueSession(item(1))
    await enqueueSession(item(2))

    const aceitos = await drainOfflineQueue()

    expect(aceitos).toBe(0)
    expect(await pendingSessions()).toHaveLength(2)
  })

  it('deve manter a fila quando o servidor recusa o lote inteiro', async () => {
    server.use(
      http.post(ROTA, () =>
        HttpResponse.json({ title: 'x', status: 400, code: 'invalid_request' }, { status: 400 }),
      ),
    )
    await enqueueSession(item(1))

    expect(await drainOfflineQueue()).toBe(0)
    expect(await pendingSessions()).toHaveLength(1)
  })

  // Resposta fora do contrato é caso real (§6.8): proxy, service worker antigo
  // ou versão de API que não é a esperada. A fila fica, e o ciclo não quebra.
  it('deve manter a fila quando a resposta vem fora do contrato', async () => {
    server.use(http.post(ROTA, () => HttpResponse.json({ nada: true })))
    await enqueueSession(item(1))

    expect(await drainOfflineQueue()).toBe(0)
    expect(await pendingSessions()).toHaveLength(1)
  })

  it('deve mandar uma vez só quando duas drenagens começam juntas', async () => {
    const lotes = aceitandoTudo()
    await enqueueSession(item(1))

    const [primeira, segunda] = await Promise.all([drainOfflineQueue(), drainOfflineQueue()])

    expect(lotes).toEqual([[item(1)]])
    expect(primeira + segunda).toBe(1)
  })

  it('deve drenar de novo depois que a drenagem anterior terminou', async () => {
    const lotes = aceitandoTudo()
    await enqueueSession(item(1))
    await drainOfflineQueue()

    await enqueueSession(item(2))
    await drainOfflineQueue()

    expect(lotes).toEqual([[item(1)], [item(2)]])
  })
})

describe('fila maior que o teto do lote', () => {
  const total = MAX_OFFLINE_BATCH_ITEMS + 1

  async function encher(): Promise<void> {
    for (let i = 0; i < total; i++) await enqueueSession(item(i))
  }

  it('deve subir em lotes sucessivos, nenhum acima do teto', async () => {
    const lotes = aceitandoTudo()
    await encher()

    await drainOfflineQueue()

    expect(lotes.map((lote) => lote.length)).toEqual([MAX_OFFLINE_BATCH_ITEMS, 1])
  })

  it('deve esvaziar a fila inteira', async () => {
    aceitandoTudo()
    await encher()

    expect(await drainOfflineQueue()).toBe(total)
    expect(await pendingSessions()).toEqual([])
  })

  // O segundo lote não pode carregar de novo o que o primeiro já entregou.
  it('não deve repetir item algum entre um lote e outro', async () => {
    const lotes = aceitandoTudo()
    await encher()

    await drainOfflineQueue()

    const ids = lotes.flat().map((i) => i.clientSessionId)
    expect(new Set(ids).size).toBe(total)
  })
})

/**
 * Navegação privada com armazenamento bloqueado (§3.4). O ciclo nunca dependeu
 * de banco no navegador, e não passa a depender: a fila simplesmente não
 * recolhe, e nada estoura.
 */
describe('navegador sem IndexedDB', () => {
  beforeEach(() => {
    vi.stubGlobal('indexedDB', undefined)
  })

  it('não deve estourar ao enfileirar', async () => {
    await expect(enqueueSession(item(1))).resolves.toBeUndefined()
  })

  it('deve responder fila vazia', async () => {
    expect(await pendingSessions()).toEqual([])
  })

  it('não deve tocar a rede ao drenar', async () => {
    const lotes = aceitandoTudo()

    expect(await drainOfflineQueue()).toBe(0)
    expect(lotes).toEqual([])
  })

  it('não deve estourar ao descartar a fila', async () => {
    await expect(clearOfflineQueue()).resolves.toBeUndefined()
  })
})
