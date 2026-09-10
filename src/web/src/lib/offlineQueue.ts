import { openDB, type IDBPDatabase } from 'idb'
import { batches, type QueuedSession } from '../domain/offline'
import { apiFetch } from './api'

/**
 * A fila de ciclos cumpridos sem servidor (§3.4).
 *
 * Guarda `{clientSessionId, startedAt, durationSeconds}` e nada além disso: a
 * fila não sabe de quem ela é, porque identificador de usuário não pode morar
 * no navegador (§7.2/A01). É por isso que sair da conta — ou perder a sessão —
 * a descarta inteira, em vez de guardá-la para o próximo login.
 *
 * Nenhuma função daqui estoura: navegação privada com armazenamento bloqueado
 * é caso real, e o ciclo nunca dependeu de banco no navegador (§6.8).
 */

const DB_NAME = 'tomatempo'
const DB_VERSION = 1
const STORE = 'offline-sessions'
const BY_CLIENT_SESSION = 'by-client-session'

/** Veredito por item (§4); `reason` é o `code` pelo qual o cliente descarta. */
interface OfflineItemResult {
  readonly clientSessionId: string
  readonly accepted: boolean
  readonly reason: string | null
}

interface OfflineBatchResult {
  readonly items: readonly OfflineItemResult[]
}

export async function enqueueSession(session: QueuedSession): Promise<void> {
  const db = await abrir()
  if (!db) return

  try {
    // Chave gerada, e não o `clientSessionId`: é o que guarda a ordem em que os
    // ciclos foram cumpridos. O índice único sobre o id cuida de o mesmo ciclo
    // nunca virar duas linhas.
    await db.put(STORE, session)
  } catch {
    // Cota estourada ou banco derrubado no meio: o ciclo já foi cumprido e
    // creditado na tela; o que se perde é o minuto que subiria depois.
  } finally {
    db.close()
  }
}

/** O que está esperando a rede, na ordem em que foi cumprido. */
export async function pendingSessions(): Promise<QueuedSession[]> {
  const db = await abrir()
  if (!db) return []

  try {
    return await db.getAll(STORE)
  } catch {
    return []
  } finally {
    db.close()
  }
}

export async function clearOfflineQueue(): Promise<void> {
  const db = await abrir()
  if (!db) return

  try {
    await db.clear(STORE)
  } catch {
    // Banco inacessível é fila inacessível: não há o que drenar dela depois.
  } finally {
    db.close()
  }
}

/**
 * Drenagem em curso. Duas idas concorrentes mandariam o mesmo item duas vezes —
 * e, embora o `clientSessionId` torne a segunda um no-op no servidor (§3.4),
 * gastar a viagem para descobrir isso não tem graça nenhuma.
 */
let emCurso: Promise<number> | null = null

/**
 * Sobe a fila para `/sessions/offline-batch` e devolve quantos itens o servidor
 * aceitou — é por esse número que a tela decide reler o dia da conta.
 *
 * O item só sai da fila **depois da resposta**: aceito ou recusado, ele sai
 * (recusa é veredito, e insistir seria bater na mesma porta para sempre); sem
 * resposta, a fila inteira fica para a próxima tentativa.
 */
export function drainOfflineQueue(): Promise<number> {
  emCurso ??= drenar().finally(() => {
    emCurso = null
  })

  return emCurso
}

async function drenar(): Promise<number> {
  const pendentes = await pendingSessions()
  let aceitos = 0

  for (const lote of batches(pendentes)) {
    let vereditos: readonly OfflineItemResult[]

    try {
      const resposta = await apiFetch<OfflineBatchResult>('/sessions/offline-batch', {
        method: 'POST',
        json: { items: lote },
      })

      vereditos = julgados(resposta)
    } catch {
      // Sem resposta ninguém sabe se o servidor creditou. A fila fica inteira,
      // e os lotes seguintes ficam para quando a rede voltar.
      return aceitos
    }

    await descartar(vereditos.map((veredito) => veredito.clientSessionId))
    aceitos += vereditos.filter((veredito) => veredito.accepted).length
  }

  return aceitos
}

/**
 * Os vereditos que vieram na resposta.
 *
 * Resposta 200 fora do contrato é caso real (§6.8) — proxy, service worker de
 * uma versão antiga, API que não é a esperada. Tratá-la como resposta vazia
 * mantém a fila para a próxima tentativa, que é o desfecho seguro: o outro
 * caminho seria descartar item que talvez não tenha sido creditado.
 */
function julgados(resposta: OfflineBatchResult): readonly OfflineItemResult[] {
  if (!Array.isArray(resposta?.items)) throw new Error('Lote respondido fora do contrato.')

  return resposta.items
}

async function descartar(ids: readonly string[]): Promise<void> {
  if (ids.length === 0) return

  const db = await abrir()
  if (!db) return

  try {
    const transacao = db.transaction(STORE, 'readwrite')
    const indice = transacao.store.index(BY_CLIENT_SESSION)

    await Promise.all([
      ...ids.map(async (id) => {
        const chave = await indice.getKey(id)
        if (chave !== undefined) await transacao.store.delete(chave)
      }),
      transacao.done,
    ])
  } catch {
    // O servidor já julgou; a fila é que não pôde ser limpa. O reenvio seguinte
    // é no-op pelo `clientSessionId`, então nada é creditado duas vezes.
  } finally {
    db.close()
  }
}

/**
 * Abre a fila; `null` quando este navegador não tem IndexedDB para dar — é o
 * caso da navegação privada com armazenamento bloqueado (§3.4).
 */
async function abrir(): Promise<IDBPDatabase | null> {
  if (!globalThis.indexedDB) return null

  try {
    return await openDB(DB_NAME, DB_VERSION, {
      upgrade(db) {
        const store = db.createObjectStore(STORE, { autoIncrement: true })
        store.createIndex(BY_CLIENT_SESSION, 'clientSessionId', { unique: true })
      },
    })
  } catch {
    return null
  }
}
