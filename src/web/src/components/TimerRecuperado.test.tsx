import 'fake-indexeddb/auto'
import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen } from '@testing-library/react'
import { HttpResponse, http } from 'msw'
import { act } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createTimerStore, type TimerStore } from '../hooks/useTimerStore'
import { clearOfflineQueue, pendingSessions } from '../lib/offlineQueue'
import { createQueryClient } from '../lib/queryClient'
import { rememberSession } from '../lib/sessionHint'
import { accountFixture } from '../test/fixtures'
import { pt } from '../test/i18nText'
import { memoryStorage } from '../test/memoryStorage'
import { server } from '../test/server'
import { TimerScreen } from './TimerScreen'

/**
 * O ciclo recuperado ao voltar para a tela (§3.2).
 *
 * Critérios da §5: "recarregar no meio do foco recupera a sessão por
 * `GET /sessions/active` e volta com o tempo certo", "a sessão recuperada volta
 * a bater e é ela que o fim do ciclo fecha", "sessão ativa no servidor com o
 * timer local parado retoma o ciclo na tela", "visitante anônimo não pergunta
 * por sessão ativa alguma", "`204` no meio de um ciclo local não mostra aviso
 * de sessão perdida e não interrompe a contagem", "ciclo que atravessa um
 * recarregamento sem resposta do servidor termina na fila offline", "recuperar
 * a sessão não a toma de outra aba", "pausa em andamento não é interrompida" e
 * "ciclo começado antes de a resposta chegar não é atropelado".
 *
 * Recarregar, aqui, é montar a tela de novo sobre o armazenamento que ficou —
 * que é exatamente o que o navegador faz.
 */

const MINUTE = 60_000
const AGORA = '2026-09-10T12:00:00.000Z'
const SESSAO = '0199b0e0-0000-7000-8000-000000000000'

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

/**
 * O armazenamento de quem fechou a aba no meio do foco: ciclo correndo desde
 * 11h50, dez minutos atrás.
 */
function focoEmAndamento(): Storage {
  return memoryStorage({
    'tomatempo:timer:v1': JSON.stringify({
      timer: {
        kind: 'focus',
        status: 'running',
        startedAt: Date.parse('2026-09-10T11:50:00.000Z'),
        endsAt: Date.parse('2026-09-10T12:15:00.000Z'),
        completedPomodoros: 1,
      },
      log: { date: '2026-09-10', focusMinutes: 25, completedPomodoros: 1 },
      runningDateKey: '2026-09-10',
    }),
  })
}

function montar(store: TimerStore): TimerStore {
  render(
    <QueryClientProvider client={createQueryClient()}>
      <TimerScreen store={store} />
    </QueryClientProvider>,
  )

  return store
}

function comConta(): void {
  rememberSession(localStorage)
  server.use(
    http.get('/api/me', () => HttpResponse.json(accountFixture())),
    http.get('/api/stats/today', () =>
      HttpResponse.json({
        localDate: '2026-09-10',
        completedPomodoros: 1,
        focusMinutes: 25,
        rankingMinutes: 25,
        goal: 4,
        goalMet: false,
      }),
    ),
  )
}

/**
 * A sessão que o servidor diz estar correndo. Começou 11h52 — de propósito
 * diferente do que o armazenamento local guarda —, então restam 17 minutos, e
 * não os 15 que a contagem local daria.
 */
function sessaoAtiva() {
  return http.get('/api/sessions/active', () =>
    HttpResponse.json({
      sessionId: SESSAO,
      startedAt: '2026-09-10T11:52:00.000Z',
      endsAt: '2026-09-10T12:17:00.000Z',
      serverNow: AGORA,
      remainingSeconds: 17 * 60,
    }),
  )
}

/** Servidor sem sessão ativa: o 204 do contrato (§4). */
function semSessaoAtiva() {
  return http.get('/api/sessions/active', () => new HttpResponse(null, { status: 204 }))
}

beforeEach(async () => {
  await clearOfflineQueue()
  vi.useFakeTimers({
    toFake: ['setTimeout', 'clearTimeout', 'setInterval', 'clearInterval', 'Date'],
  })
  vi.setSystemTime(Date.parse(AGORA))
})

afterEach(() => {
  vi.useRealTimers()
})

describe('a sessão que o servidor diz estar correndo', () => {
  it('deve voltar com o tempo que o servidor carimbou', async () => {
    comConta()
    server.use(sessaoAtiva())

    montar(createTimerStore({ storage: focoEmAndamento() }))
    await assentar()

    expect(screen.getByRole('timer')).toHaveTextContent('17:00')
  })

  it('deve seguir contando a partir do tempo recuperado', async () => {
    comConta()
    server.use(sessaoAtiva())

    montar(createTimerStore({ storage: focoEmAndamento() }))
    await assentar()
    await avancar(2 * MINUTE)

    expect(screen.getByRole('timer')).toHaveTextContent('15:00')
  })

  // Sem o id recuperado não há batimento, e a sessão morreria de fome com a
  // pessoa trabalhando na frente da tela (§3.2).
  it('deve voltar a bater na sessão recuperada', async () => {
    comConta()
    let batidas = 0
    server.use(
      sessaoAtiva(),
      http.post(`/api/sessions/${SESSAO}/beat`, () => {
        batidas += 1
        return HttpResponse.json({ ok: true, serverNow: AGORA, remainingSeconds: 900 })
      }),
    )

    montar(createTimerStore({ storage: focoEmAndamento() }))
    await assentar()
    await avancar(90_000)

    expect(batidas).toBe(3)
  })

  it('deve fechar a sessão recuperada ao abandonar', async () => {
    comConta()
    let abandonou = false
    server.use(
      sessaoAtiva(),
      http.post(`/api/sessions/${SESSAO}/beat`, () =>
        HttpResponse.json({ ok: true, serverNow: AGORA, remainingSeconds: 900 }),
      ),
      http.post(`/api/sessions/${SESSAO}/abandon`, () => {
        abandonou = true
        return HttpResponse.json({ countedMinutes: 12, discarded: false })
      }),
    )

    montar(createTimerStore({ storage: focoEmAndamento() }))
    await assentar()
    fireEvent.click(screen.getByRole('button', { name: pt.action.abandon }))
    await assentar()

    expect(abandonou).toBe(true)
    expect(await pendingSessions()).toEqual([])
  })

  it('deve fechar a sessão recuperada quando o ciclo chega ao fim', async () => {
    comConta()
    let concluiu = false
    server.use(
      sessaoAtiva(),
      http.post(`/api/sessions/${SESSAO}/beat`, () =>
        HttpResponse.json({ ok: true, serverNow: AGORA, remainingSeconds: 60 }),
      ),
      http.post(`/api/sessions/${SESSAO}/complete`, () => {
        concluiu = true
        return HttpResponse.json({ countedMinutes: 25, completedToday: 2, goal: 4, goalMet: false })
      }),
    )

    montar(createTimerStore({ storage: focoEmAndamento() }))
    await assentar()
    await avancar(17 * MINUTE)

    expect(concluiu).toBe(true)
    expect(await pendingSessions()).toEqual([])
  })

  // Armazenamento perdido ou app aberto em outro dispositivo: a tela não sabe
  // de ciclo nenhum, e o servidor sabe.
  it('deve retomar o ciclo com o timer local parado', async () => {
    comConta()
    server.use(sessaoAtiva())

    montar(createTimerStore({ storage: memoryStorage() }))
    await assentar()

    expect(screen.getByRole('timer')).toHaveTextContent('17:00')
    expect(screen.getByRole('button', { name: pt.action.abandon })).toBeInTheDocument()
  })

  // Quem assume a sessão de outra aba é o `start` (§3.3). Se abrir a tela
  // bastasse, uma segunda aba aberta por engano derrubaria quem trabalha.
  it('não deve abrir sessão nova ao recuperar', async () => {
    comConta()
    let abriu = false
    server.use(
      sessaoAtiva(),
      http.post('/api/sessions/start', () => {
        abriu = true
        return new HttpResponse(null, { status: 204 })
      }),
    )

    montar(createTimerStore({ storage: focoEmAndamento() }))
    await assentar()

    expect(abriu).toBe(false)
  })

  it('não deve interromper a pausa em andamento', async () => {
    comConta()
    server.use(sessaoAtiva())

    montar(
      createTimerStore({
        storage: memoryStorage({
          'tomatempo:timer:v1': JSON.stringify({
            timer: {
              kind: 'shortBreak',
              status: 'running',
              startedAt: Date.parse('2026-09-10T11:58:00.000Z'),
              endsAt: Date.parse('2026-09-10T12:03:00.000Z'),
              completedPomodoros: 1,
            },
            log: { date: '2026-09-10', focusMinutes: 25, completedPomodoros: 1 },
            runningDateKey: '2026-09-10',
          }),
        }),
      }),
    )
    await assentar()

    expect(screen.getByRole('timer')).toHaveTextContent('03:00')
  })

  it('não deve atropelar o ciclo que a pessoa começou antes da resposta', async () => {
    comConta()
    server.use(
      sessaoAtiva(),
      http.post('/api/sessions/start', () => HttpResponse.error()),
    )

    montar(createTimerStore({ storage: memoryStorage() }))
    // Sem assentar: a resposta da sessão ativa ainda está no ar quando o dedo
    // cai no botão.
    fireEvent.click(screen.getByRole('button', { name: pt.action.startFocus }))
    await assentar()

    // 25 minutos cheios, e não os 17 da sessão recuperada.
    expect(screen.getByRole('timer')).toHaveTextContent('25:00')
  })
})

describe('o visitante anônimo', () => {
  it('não deve perguntar por sessão ativa alguma', async () => {
    const perguntas: string[] = []
    server.use(
      http.all('/api/*', ({ request }) => {
        perguntas.push(new URL(request.url).pathname)
        return new HttpResponse(null, { status: 204 })
      }),
    )

    montar(createTimerStore({ storage: focoEmAndamento() }))
    await assentar()

    expect(perguntas).toEqual([])
  })
})

/**
 * O servidor sem sessão ativa é ambíguo: pode ser sessão já apurada pelo
 * varredor ou ciclo que nasceu offline. O ciclo na tela continua, e o desfecho
 * é a fila — quem impede o crédito em dobro é a sobreposição, no servidor
 * (§3.4).
 */
describe('o servidor sem sessão ativa', () => {
  it('não deve mostrar aviso de sessão perdida', async () => {
    comConta()
    server.use(semSessaoAtiva())

    montar(createTimerStore({ storage: focoEmAndamento() }))
    await assentar()

    expect(screen.queryByTestId('session-lost')).not.toBeInTheDocument()
  })

  it('não deve interromper a contagem do ciclo', async () => {
    comConta()
    server.use(semSessaoAtiva())

    montar(createTimerStore({ storage: focoEmAndamento() }))
    await assentar()
    await avancar(5 * MINUTE)

    expect(screen.getByRole('timer')).toHaveTextContent('10:00')
  })

  it('deve mandar para a fila o ciclo que termina depois', async () => {
    comConta()
    server.use(semSessaoAtiva())

    montar(createTimerStore({ storage: focoEmAndamento() }))
    await assentar()
    await avancar(15 * MINUTE)

    const fila = await pendingSessions()
    expect(fila).toHaveLength(1)
    expect(fila[0]?.startedAt).toBe('2026-09-10T11:50:00.000Z')
  })

  it('deve mandar para a fila o parcial de quem abandona depois', async () => {
    comConta()
    server.use(semSessaoAtiva())

    montar(createTimerStore({ storage: focoEmAndamento() }))
    await assentar()
    await avancar(2 * MINUTE)
    fireEvent.click(screen.getByRole('button', { name: pt.action.abandon }))
    await assentar()

    // 12 minutos: os 10 que já corriam quando a tela abriu, mais os 2 de agora.
    expect((await pendingSessions())[0]?.durationSeconds).toBe(12 * 60)
  })

  it('deve tratar a pergunta sem resposta como ciclo sem servidor', async () => {
    comConta()
    server.use(http.get('/api/sessions/active', () => HttpResponse.error()))

    montar(createTimerStore({ storage: focoEmAndamento() }))
    await assentar()
    await avancar(15 * MINUTE)

    expect(await pendingSessions()).toHaveLength(1)
  })

  it('não deve enfileirar nada quando não há ciclo correndo', async () => {
    comConta()
    server.use(semSessaoAtiva())

    montar(createTimerStore({ storage: memoryStorage() }))
    await assentar()

    expect(await pendingSessions()).toEqual([])
  })
})
