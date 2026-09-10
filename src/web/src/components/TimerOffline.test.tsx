import 'fake-indexeddb/auto'
import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { HttpResponse, http } from 'msw'
import { act } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { QueuedSession } from '../domain/offline'
import { createTimerStore, type TimerStore } from '../hooks/useTimerStore'
import { clearOfflineQueue, enqueueSession, pendingSessions } from '../lib/offlineQueue'
import { createQueryClient } from '../lib/queryClient'
import { hasSessionHint, rememberSession } from '../lib/sessionHint'
import { accountFixture, freezeLedgerFixture } from '../test/fixtures'
import { pt } from '../test/i18nText'
import { memoryStorage } from '../test/memoryStorage'
import { renderRoute } from '../test/renderRoute'
import { server } from '../test/server'
import { TimerScreen } from './TimerScreen'

/**
 * O ciclo cumprido sem servidor e a fila que o leva até a conta (§3.4).
 *
 * Critérios da §5: "ciclo cumprido offline entra na fila com `clientSessionId`
 * próprio e drena para `/sessions/offline-batch` quando a conexão volta",
 * "ciclo do anônimo não entra na fila, mesmo offline", "ciclo que abriu sessão
 * no servidor não entra na fila, nem quando o fechamento não vai", "abandono
 * offline entra com o parcial truncado; abaixo de 5 minutos não entra", "app
 * aberto online com fila pendente drena sem esperar evento de reconexão",
 * "sair da conta descarta a fila offline" e "sessão derrubada pelo 401
 * `unauthenticated` descarta a fila".
 */

const MINUTE = 60_000
const AGORA = '2026-09-10T12:00:00.000Z'
const SESSAO = '0199b0e0-0000-7000-8000-000000000000'
const ROTA_LOTE = '/api/sessions/offline-batch'
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

async function assentar(voltas = 20): Promise<void> {
  for (let i = 0; i < voltas; i++) {
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0)
    })
  }
}

async function avancar(ms: number): Promise<void> {
  act(() => void vi.advanceTimersByTime(ms))
  await assentar()
}

function montar(store: TimerStore = createTimerStore({ storage: memoryStorage() })): TimerStore {
  render(
    <QueryClientProvider client={createQueryClient()}>
      <TimerScreen store={store} />
    </QueryClientProvider>,
  )

  return store
}

function iniciar(): void {
  fireEvent.click(screen.getByRole('button', { name: pt.action.startFocus }))
}

function abandonar(): void {
  fireEvent.click(screen.getByRole('button', { name: pt.action.abandon }))
}

/** Conta de pé neste navegador, com a rede inteira fora do ar — modo avião. */
function semRede(): void {
  rememberSession(localStorage)
  server.use(
    http.get('/api/me', () => HttpResponse.error()),
    http.post('/api/sessions/start', () => HttpResponse.error()),
  )
}

/** Conta de pé e servidor respondendo, menos a abertura de sessão. */
function comContaSemSessao(hoje = { completedPomodoros: 0, focusMinutes: 0 }): void {
  rememberSession(localStorage)
  server.use(
    http.get('/api/me', () => HttpResponse.json(accountFixture())),
    http.get('/api/stats/today', () =>
      HttpResponse.json({
        localDate: '2026-09-10',
        completedPomodoros: hoje.completedPomodoros,
        focusMinutes: hoje.focusMinutes,
        rankingMinutes: hoje.focusMinutes,
        goal: 4,
        goalMet: false,
      }),
    ),
    http.post('/api/sessions/start', () => HttpResponse.error()),
  )
}

/** Servidor aceitando o lote; devolve o que chegou em cada ida. */
function aceitandoOLote() {
  const lotes: QueuedSession[][] = []

  server.use(
    http.post(ROTA_LOTE, async ({ request }) => {
      const corpo = (await request.json()) as { items: QueuedSession[] }
      lotes.push(corpo.items)

      return HttpResponse.json({
        items: corpo.items.map((i) => ({
          clientSessionId: i.clientSessionId,
          accepted: true,
          reason: null,
        })),
      })
    }),
  )

  return lotes
}

function reconectar(): void {
  act(() => void globalThis.dispatchEvent(new Event('online')))
}

function itemDaFila(overrides: Partial<QueuedSession> = {}): QueuedSession {
  return {
    clientSessionId: SESSAO,
    startedAt: '2026-09-10T09:00:00.000Z',
    durationSeconds: 25 * 60,
    ...overrides,
  }
}

beforeEach(async () => {
  await clearOfflineQueue()
})

describe('o ciclo cumprido sem servidor', () => {
  beforeEach(() => {
    vi.useFakeTimers({
      toFake: ['setTimeout', 'clearTimeout', 'setInterval', 'clearInterval', 'Date'],
    })
    vi.setSystemTime(Date.parse(AGORA))
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('deve entrar na fila com clientSessionId próprio', async () => {
    semRede()

    montar()
    await assentar()
    iniciar()
    await assentar()
    await avancar(25 * MINUTE)

    const fila = await pendingSessions()
    expect(fila).toHaveLength(1)
    expect(fila[0]?.clientSessionId).toMatch(UUID)
  })

  it('deve entrar na fila com o instante do início e os 25 minutos cheios', async () => {
    semRede()

    montar()
    await assentar()
    iniciar()
    await assentar()
    await avancar(25 * MINUTE)

    expect(await pendingSessions()).toEqual([
      { clientSessionId: expect.stringMatching(UUID), startedAt: AGORA, durationSeconds: 25 * 60 },
    ])
  })

  it('deve dar um clientSessionId diferente a cada ciclo', async () => {
    semRede()

    montar()
    await assentar()
    iniciar()
    await assentar()
    await avancar(25 * MINUTE)
    fireEvent.click(screen.getByRole('button', { name: pt.action.startShortBreak }))
    await avancar(5 * MINUTE)
    fireEvent.click(screen.getByRole('button', { name: pt.action.startFocus }))
    await assentar()
    await avancar(25 * MINUTE)

    const ids = (await pendingSessions()).map((i) => i.clientSessionId)
    expect(new Set(ids).size).toBe(2)
  })

  // A fila só recolhe ciclo de quem tem conta: o do anônimo é dele e não migra
  // (§3.10).
  it('não deve recolher o ciclo do anônimo', async () => {
    montar()
    await assentar()
    iniciar()
    await assentar()
    await avancar(25 * MINUTE)

    expect(await pendingSessions()).toEqual([])
  })

  // Quem abriu sessão tem o varredor para encerrá-la (§3.2); enfileirar aqui
  // contaria o mesmo ciclo duas vezes.
  it('não deve recolher o ciclo que abriu sessão no servidor', async () => {
    rememberSession(localStorage)
    server.use(
      http.get('/api/me', () => HttpResponse.json(accountFixture())),
      http.get('/api/stats/today', () => HttpResponse.error()),
      http.post('/api/sessions/start', () =>
        HttpResponse.json({
          sessionId: SESSAO,
          startedAt: AGORA,
          endsAt: '2026-09-10T12:25:00.000Z',
          serverNow: AGORA,
        }),
      ),
      http.post(`/api/sessions/${SESSAO}/beat`, () =>
        HttpResponse.json({ ok: true, serverNow: AGORA, remainingSeconds: 900 }),
      ),
      // O fechamento que não vai: quem encerra a sessão é o varredor.
      http.post(`/api/sessions/${SESSAO}/complete`, () => HttpResponse.error()),
    )

    montar()
    await assentar()
    iniciar()
    await assentar()
    await avancar(25 * MINUTE)

    expect(await pendingSessions()).toEqual([])
  })

  it('deve entrar na fila com o parcial truncado de quem abandonou', async () => {
    semRede()

    montar()
    await assentar()
    iniciar()
    await assentar()
    await avancar(12 * MINUTE + 30_000)
    abandonar()
    await assentar()

    expect((await pendingSessions())[0]?.durationSeconds).toBe(12 * 60)
  })

  it('não deve recolher o parcial abaixo de 5 minutos', async () => {
    semRede()

    montar()
    await assentar()
    iniciar()
    await assentar()
    await avancar(3 * MINUTE)
    abandonar()
    await assentar()

    expect(await pendingSessions()).toEqual([])
  })

  it('não deve recolher a pausa', async () => {
    semRede()

    montar()
    await assentar()
    iniciar()
    await assentar()
    await avancar(25 * MINUTE)
    fireEvent.click(screen.getByRole('button', { name: pt.action.startShortBreak }))
    await avancar(5 * MINUTE)

    expect(await pendingSessions()).toHaveLength(1)
  })
})

describe('a volta da conexão', () => {
  beforeEach(() => {
    vi.useFakeTimers({
      toFake: ['setTimeout', 'clearTimeout', 'setInterval', 'clearInterval', 'Date'],
    })
    vi.setSystemTime(Date.parse(AGORA))
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('deve drenar a fila para o lote offline', async () => {
    comContaSemSessao()
    const lotes = aceitandoOLote()

    montar()
    await assentar()
    iniciar()
    await assentar()
    await avancar(25 * MINUTE)

    reconectar()
    await assentar()

    expect(lotes).toHaveLength(1)
    expect(lotes[0]?.[0]?.durationSeconds).toBe(25 * 60)
    expect(await pendingSessions()).toEqual([])
  })

  it('não deve drenar a fila de quem não tem conta neste navegador', async () => {
    const lotes = aceitandoOLote()
    await enqueueSession(itemDaFila())

    montar()
    await assentar()
    reconectar()
    await assentar()

    expect(lotes).toEqual([])
  })

  // Depois de o lote entrar, o dia da conta é outro — e ele vem do servidor,
  // nunca de soma feita aqui (REGRAS §2).
  it('deve reler o dia da conta depois de drenar', async () => {
    rememberSession(localStorage)
    let idas = 0
    server.use(
      http.get('/api/me', () => HttpResponse.json(accountFixture())),
      http.get('/api/stats/today', () => {
        idas += 1
        return HttpResponse.json({
          localDate: '2026-09-10',
          completedPomodoros: idas === 1 ? 0 : 3,
          focusMinutes: idas === 1 ? 0 : 75,
          rankingMinutes: 0,
          goal: 4,
          goalMet: false,
        })
      }),
    )
    aceitandoOLote()
    await enqueueSession(itemDaFila())

    montar()
    await assentar()
    reconectar()
    await assentar()

    expect(screen.getByTestId('today-summary')).toHaveTextContent('3 pomodoros')
  })

  // Ciclo cumprido com a aba fechada não pode esperar um evento `online` que
  // talvez nunca chegue (§3.4).
  it('deve drenar a fila pendente já na abertura do app', async () => {
    comContaSemSessao()
    const lotes = aceitandoOLote()
    await enqueueSession(itemDaFila())

    montar()
    await assentar()

    expect(lotes).toHaveLength(1)
  })

  // `navigator.onLine` é otimista, mas com a interface de rede caída não há
  // sequer o que tentar (§3.4).
  it('não deve tentar drenar na abertura com a rede caída', async () => {
    comContaSemSessao()
    const lotes = aceitandoOLote()
    await enqueueSession(itemDaFila())
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)

    montar()
    await assentar()

    expect(lotes).toEqual([])
  })

  it('não deve tocar a rede na abertura quando a fila está vazia', async () => {
    comContaSemSessao()
    const lotes = aceitandoOLote()

    montar()
    await assentar()

    expect(lotes).toEqual([])
  })
})

/**
 * A fila não sabe de quem ela é (§7.2/A01): sobrevivendo à saída, ela drenaria
 * na próxima conta que entrasse neste navegador.
 */
describe('o fim da sessão local', () => {
  function contaEmPe() {
    rememberSession(localStorage)
    server.use(
      http.get('/api/me', () => HttpResponse.json(accountFixture())),
      http.get('/api/me/freezes', () => HttpResponse.json(freezeLedgerFixture())),
    )
  }

  it('deve descartar a fila ao sair da conta', async () => {
    contaEmPe()
    server.use(http.post('/api/auth/logout', () => new HttpResponse(null, { status: 204 })))
    await enqueueSession(itemDaFila())

    renderRoute({ path: '/conta' })
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: pt.account.signOut }))

    await waitFor(async () => expect(await pendingSessions()).toEqual([]))
  })

  // Sair sem rede não pode deixar a fila para trás: o navegador fica no mesmo
  // estado de quem saiu com rede.
  it('deve descartar a fila mesmo quando o logout não vai', async () => {
    contaEmPe()
    server.use(http.post('/api/auth/logout', () => HttpResponse.error()))
    await enqueueSession(itemDaFila())

    renderRoute({ path: '/conta' })
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: pt.account.signOut }))

    await waitFor(async () => expect(await pendingSessions()).toEqual([]))
  })

  // Cookie vencido ou revogado deixa o navegador no mesmo estado do logout
  // (REGRAS §8, decisão de 10/09/2026).
  it('deve descartar a fila quando a sessão cai pelo 401', async () => {
    rememberSession(localStorage)
    server.use(
      http.get('/api/me', () =>
        HttpResponse.json({ title: 'x', status: 401, code: 'unauthenticated' }, { status: 401 }),
      ),
    )
    await enqueueSession(itemDaFila())

    renderRoute({ path: '/conta' })

    await waitFor(() => expect(hasSessionHint(localStorage)).toBe(false))
    await waitFor(async () => expect(await pendingSessions()).toEqual([]))
  })

  // Fechar a aba sem sair não passa por logout nem por 401: a fila de quem
  // estava aqui antes continuaria de pé, e a próxima conta a entrar a receberia
  // (REGRAS §8, decisão de 10/09/2026).
  it('deve descartar a fila de quem ficou para trás ao entrar numa conta', async () => {
    server.use(
      http.post('/api/auth/login', () => new HttpResponse(null, { status: 204 })),
      http.get('/api/me', () => HttpResponse.json(accountFixture())),
      http.get('/api/stats/today', () =>
        HttpResponse.json({
          localDate: '2026-09-10',
          completedPomodoros: 0,
          focusMinutes: 0,
          rankingMinutes: 0,
          goal: 4,
          goalMet: false,
        }),
      ),
    )
    await enqueueSession(itemDaFila())

    renderRoute({ path: '/entrar' })
    const user = userEvent.setup()

    await user.type(screen.getByLabelText(pt.account.emailLabel), 'outra@exemplo.test')
    await user.type(screen.getByLabelText(pt.account.passwordLabel), 'senha-bem-longa')
    await user.click(screen.getByRole('button', { name: pt.account.signInSubmit }))

    await waitFor(async () => expect(await pendingSessions()).toEqual([]))
  })

  it('deve descartar a fila ao criar uma conta', async () => {
    server.use(
      http.post('/api/auth/register', () => new HttpResponse(null, { status: 202 })),
      http.get('/api/me', () => HttpResponse.json(accountFixture())),
    )
    await enqueueSession(itemDaFila())

    renderRoute({ path: '/cadastrar' })
    const user = userEvent.setup()

    await user.type(screen.getByLabelText(pt.account.emailLabel), 'nova@exemplo.test')
    await user.type(screen.getByLabelText(pt.account.passwordLabel), 'senha-bem-longa')
    await user.type(screen.getByLabelText(pt.account.nicknameLabel), 'novapessoa')
    await user.click(screen.getByRole('button', { name: pt.account.signUpSubmit }))

    await waitFor(async () => expect(await pendingSessions()).toEqual([]))
  })

  // Errar a senha na exclusão também responde 401, com `invalid_credentials`, e
  // não pode custar os ciclos pendentes de ninguém (§5).
  it('não deve descartar a fila quando o 401 é de senha errada', async () => {
    contaEmPe()
    server.use(
      http.delete('/api/me', () =>
        HttpResponse.json(
          { title: 'x', status: 401, code: 'invalid_credentials' },
          { status: 401 },
        ),
      ),
    )
    await enqueueSession(itemDaFila())

    renderRoute({ path: '/conta' })
    const user = userEvent.setup()

    await user.type(await screen.findByLabelText(pt.account.deletePasswordLabel), 'errada12')
    await user.click(screen.getByRole('button', { name: pt.account.deleteSubmit }))

    expect(await screen.findByRole('alert')).toHaveTextContent(pt.error.invalid_credentials)
    expect(await pendingSessions()).toHaveLength(1)
  })

  it('deve descartar a fila ao excluir a conta', async () => {
    contaEmPe()
    server.use(http.delete('/api/me', () => new HttpResponse(null, { status: 204 })))
    await enqueueSession(itemDaFila())

    renderRoute({ path: '/conta' })
    const user = userEvent.setup()

    await user.type(await screen.findByLabelText(pt.account.deletePasswordLabel), 'senha-boa8')
    await user.click(screen.getByRole('button', { name: pt.account.deleteSubmit }))

    await waitFor(async () => expect(await pendingSessions()).toEqual([]))
  })
})
