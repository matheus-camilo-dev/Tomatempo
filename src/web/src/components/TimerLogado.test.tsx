import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { HttpResponse, http } from 'msw'
import { act } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createTimerStore, type TimerStore } from '../hooks/useTimerStore'
import { createQueryClient } from '../lib/queryClient'
import { rememberSession } from '../lib/sessionHint'
import { accountFixture } from '../test/fixtures'
import { pt } from '../test/i18nText'
import { memoryStorage } from '../test/memoryStorage'
import { server } from '../test/server'
import { TimerScreen } from './TimerScreen'

/**
 * O timer de quem está logado (§3.2, §3.3). Critérios da §5: "visitante anônimo
 * continua sem chamada alguma de sessão", "iniciar o ciclo com sessão de pé abre
 * a sessão no servidor, e o tempo restante exibido passa a vir dos instantes que
 * o servidor carimbou", "concluir o ciclo chama `complete`, e o dia mostrado
 * passa a ser o que a API devolveu", "abandonar chama `abandon` e mostra o
 * parcial que o servidor creditou — inclusive o zero de quem parou antes dos 5
 * minutos" e "o dia de quem está logado vem de `GET /stats/today` [...] e o
 * histórico local nunca é somado a ele".
 *
 * Os números que o servidor devolve aqui são de propósito diferentes dos que a
 * conta local daria: é assim que o teste prova de qual das duas fontes a tela
 * está lendo.
 */

const MINUTE = 60_000

/** Meio-dia UTC no servidor; a máquina do teste começa no mesmo instante. */
const SERVIDOR_AGORA = '2026-09-03T12:00:00.000Z'
const SESSAO = '0199b0e0-0000-7000-8000-000000000000'

/**
 * `toFake` restrito a timers e `Date`: com `queueMicrotask` e `setImmediate`
 * falsos, a resposta do MSW nunca chega e o teste trava. Pelo mesmo motivo o
 * flush é `advanceTimersByTimeAsync` dentro de `act`, e não `waitFor` (§6.4).
 */
beforeEach(() => {
  vi.useFakeTimers({
    toFake: ['setTimeout', 'clearTimeout', 'setInterval', 'clearInterval', 'Date'],
  })
  vi.setSystemTime(Date.parse(SERVIDOR_AGORA))
})

afterEach(() => {
  vi.useRealTimers()
})

/** Deixa a rede responder e o React repintar. */
async function assentar(voltas = 20): Promise<void> {
  // Várias voltas e não uma: `/me` e `/stats/today` são encadeados — o segundo
  // só é disparado depois que o primeiro responde —, e cada requisição consome
  // mais de um giro atravessando o interceptador do MSW. Com o relógio parado
  // não há `waitFor` que sirva (§6.4), então a espera é este laço.
  for (let i = 0; i < voltas; i++) {
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0)
    })
  }
}

/**
 * Avança o relógio deixando o tique do timer rodar, e então assenta a rede.
 *
 * Avanço síncrono, como no resto da suíte do timer: a variante assíncrona gira
 * o laço de eventos uma vez por tique — seis mil vezes em vinte e cinco minutos
 * de ciclo — e estoura o tempo do runner sem simular nada a mais.
 */
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

/** Conta de pé, com o dia que o servidor apurou. */
function logado(dia: { completedPomodoros: number; focusMinutes: number; goal?: number }): void {
  rememberSession(localStorage)
  server.use(
    http.get('/api/me', () => HttpResponse.json(accountFixture())),
    http.get('/api/stats/today', () =>
      HttpResponse.json({
        localDate: '2026-09-03',
        completedPomodoros: dia.completedPomodoros,
        focusMinutes: dia.focusMinutes,
        rankingMinutes: dia.focusMinutes,
        goal: dia.goal ?? 4,
        goalMet: false,
      }),
    ),
  )
}

function iniciar(): void {
  fireEvent.click(screen.getByRole('button', { name: pt.action.startFocus }))
}

function abandonar(): void {
  fireEvent.click(screen.getByRole('button', { name: pt.action.abandon }))
}

describe('visitante anônimo', () => {
  it('deve mostrar o dia local, e não o da conta que não existe', async () => {
    montar(
      createTimerStore({
        storage: memoryStorage({
          'tomatempo:timer:v1': JSON.stringify({
            timer: { kind: 'focus', status: 'idle', startedAt: null, endsAt: null, completedPomodoros: 2 },
            log: { date: '2026-09-03', focusMinutes: 50, completedPomodoros: 2 },
            runningDateKey: '2026-09-03',
          }),
        }),
      }),
    )
    // Sem conta não há query alguma para assentar: um giro basta, e `waitFor`
    // não serve com o relógio parado (§6.4).
    await assentar()

    expect(screen.getByTestId('today-summary')).toHaveTextContent('2 pomodoros')
    expect(screen.getByTestId('today-summary')).toHaveTextContent('50 min')
  })

  it('não deve tocar a API em ciclo algum', async () => {
    const chamadas: string[] = []
    server.use(
      http.all('/api/*', ({ request }) => {
        chamadas.push(new URL(request.url).pathname)
        return new HttpResponse(null, { status: 204 })
      }),
    )

    montar()

    iniciar()
    await avancar(3 * MINUTE)
    abandonar()
    await assentar()

    iniciar()
    await avancar(25 * MINUTE)

    expect(chamadas).toEqual([])
  })
})

describe('sessão no servidor', () => {
  it('deve abrir a sessão ao iniciar o foco', async () => {
    logado({ completedPomodoros: 0, focusMinutes: 0 })
    let abriu = false
    server.use(
      http.post('/api/sessions/start', () => {
        abriu = true
        return HttpResponse.json({
          sessionId: SESSAO,
          startedAt: SERVIDOR_AGORA,
          endsAt: '2026-09-03T12:25:00.000Z',
          serverNow: SERVIDOR_AGORA,
        })
      }),
    )

    montar()
    await assentar()

    iniciar()
    await assentar()

    expect(abriu).toBe(true)
  })

  // Adiantar o relógio da máquina não pode encurtar o ciclo (§3.2): quem manda
  // no fim é o carimbo do servidor.
  it('deve contar pelo relógio do servidor, e não pelo da máquina', async () => {
    logado({ completedPomodoros: 0, focusMinutes: 0 })
    let abriu = false
    server.use(
      http.post('/api/sessions/start', () => {
        abriu = true
        return HttpResponse.json({
          sessionId: SESSAO,
          startedAt: SERVIDOR_AGORA,
          endsAt: '2026-09-03T12:25:00.000Z',
          serverNow: SERVIDOR_AGORA,
        })
      }),
    )

    // A máquina acha que já são 12h10 — dez minutos à frente do servidor.
    vi.setSystemTime(Date.parse('2026-09-03T12:10:00.000Z'))
    montar()
    await assentar()

    iniciar()
    await assentar()

    // Sem o desconto, ler `endsAt` no relógio da máquina daria 15:00.
    expect(abriu).toBe(true)
    expect(screen.getByRole('timer')).toHaveTextContent('25:00')

    await avancar(5 * MINUTE)
    expect(screen.getByRole('timer')).toHaveTextContent('20:00')
  })
})

describe('o dia da conta', () => {
  // Relógio de verdade aqui: nenhum destes casos dirige o tempo, e o React
  // Query não assenta com o relógio parado — o laço de `advanceTimersByTimeAsync`
  // gira sem nunca disparar o agendador dele (§6.4).
  beforeEach(() => {
    vi.useRealTimers()
  })

  it('deve vir do servidor, e não da contagem local', async () => {
    logado({ completedPomodoros: 7, focusMinutes: 180 })

    // Histórico local com números próprios: se aparecerem na tela, ou se forem
    // somados aos do servidor, a fonte está errada.
    montar(
      createTimerStore({
        storage: memoryStorage({
          'tomatempo:timer:v1': JSON.stringify({
            timer: { kind: 'focus', status: 'idle', startedAt: null, endsAt: null, completedPomodoros: 2 },
            log: { date: '2026-09-03', focusMinutes: 50, completedPomodoros: 2 },
            runningDateKey: '2026-09-03',
          }),
        }),
      }),
    )
    await waitFor(() =>
      expect(screen.getByTestId('today-summary')).toHaveTextContent('7 pomodoros'),
    )
    expect(screen.getByTestId('today-summary')).toHaveTextContent('180 min')
  })

  // Mostrar o dia do anônimo para quem tem conta, mesmo por um instante, é
  // mostrar número de outra pessoa (REGRAS §2).
  it('não deve mostrar o dia local enquanto o do servidor não chega', async () => {
    logado({ completedPomodoros: 7, focusMinutes: 180 })

    montar(
      createTimerStore({
        storage: memoryStorage({
          'tomatempo:timer:v1': JSON.stringify({
            timer: { kind: 'focus', status: 'idle', startedAt: null, endsAt: null, completedPomodoros: 2 },
            log: { date: '2026-09-03', focusMinutes: 50, completedPomodoros: 2 },
            runningDateKey: '2026-09-03',
          }),
        }),
      }),
    )

    expect(screen.getByTestId('today-summary')).not.toHaveTextContent('2 pomodoros')
    expect(screen.getByTestId('today-summary')).not.toHaveTextContent('50 min')

    await waitFor(() =>
      expect(screen.getByTestId('today-summary')).toHaveTextContent('7 pomodoros'),
    )
  })

})

describe('servidor fora do ar', () => {
  // O timer é offline-first desde a fase 1: a conversa com o servidor é o que
  // rende minuto de ranking, não o que autoriza o ciclo a começar (§3.4).
  it('deve começar o ciclo pelo relógio local, quando a sessão não abre', async () => {
    logado({ completedPomodoros: 0, focusMinutes: 0 })
    server.use(http.post('/api/sessions/start', () => HttpResponse.error()))

    montar()
    await assentar()

    iniciar()
    await assentar()

    expect(screen.getByRole('timer')).toHaveTextContent('25:00')

    await avancar(5 * MINUTE)
    expect(screen.getByRole('timer')).toHaveTextContent('20:00')
    expect(screen.getByRole('button', { name: pt.action.abandon })).toBeInTheDocument()
  })

  it('deve encerrar o ciclo, quando o fechamento da sessão não vai', async () => {
    logado({ completedPomodoros: 0, focusMinutes: 0 })
    server.use(
      http.post('/api/sessions/start', () =>
        HttpResponse.json({
          sessionId: SESSAO,
          startedAt: SERVIDOR_AGORA,
          endsAt: '2026-09-03T12:25:00.000Z',
          serverNow: SERVIDOR_AGORA,
        }),
      ),
      http.post(`/api/sessions/${SESSAO}/abandon`, () => HttpResponse.error()),
    )

    montar()
    await assentar()

    iniciar()
    await assentar()
    await avancar(10 * MINUTE)
    abandonar()
    await assentar()

    // De volta ao início, pronto para o próximo — e não travado no ciclo que
    // o servidor não confirmou.
    expect(screen.getByRole('button', { name: pt.action.startFocus })).toBeInTheDocument()
    expect(screen.getByRole('timer')).toHaveTextContent('25:00')
  })
})

describe('fim do ciclo', () => {
  it('deve chamar complete e mostrar o dia que a API devolveu', async () => {
    logado({ completedPomodoros: 0, focusMinutes: 0 })
    let concluiu = false
    server.use(
      http.post('/api/sessions/start', () =>
        HttpResponse.json({
          sessionId: SESSAO,
          startedAt: SERVIDOR_AGORA,
          endsAt: '2026-09-03T12:25:00.000Z',
          serverNow: SERVIDOR_AGORA,
        }),
      ),
      http.post(`/api/sessions/${SESSAO}/complete`, () => {
        concluiu = true
        return HttpResponse.json({ countedMinutes: 25, completedToday: 7, goal: 4, goalMet: true })
      }),
      // O dia relido depois de concluir: números que a conta local não daria.
      http.get('/api/stats/today', () =>
        HttpResponse.json({
          localDate: '2026-09-03',
          completedPomodoros: 7,
          focusMinutes: 180,
          rankingMinutes: 180,
          goal: 4,
          goalMet: true,
        }),
      ),
    )

    montar()
    await assentar()

    iniciar()
    await assentar()
    await avancar(25 * MINUTE)

    expect(concluiu).toBe(true)
    expect(screen.getByTestId('today-summary')).toHaveTextContent('7 pomodoros')
    expect(screen.getByTestId('today-summary')).toHaveTextContent('180 min')
  })

  it('deve chamar abandon e mostrar o parcial que o servidor creditou', async () => {
    logado({ completedPomodoros: 0, focusMinutes: 0 })
    let abandonou = false
    server.use(
      http.post('/api/sessions/start', () =>
        HttpResponse.json({
          sessionId: SESSAO,
          startedAt: SERVIDOR_AGORA,
          endsAt: '2026-09-03T12:25:00.000Z',
          serverNow: SERVIDOR_AGORA,
        }),
      ),
      http.post(`/api/sessions/${SESSAO}/abandon`, () => {
        abandonou = true
        return HttpResponse.json({ countedMinutes: 18, discarded: false })
      }),
      // 18, e não os 20 que o relógio local daria: o servidor truncou no último
      // heartbeat, e é o número dele que vale (§3.2).
      http.get('/api/stats/today', () =>
        HttpResponse.json({
          localDate: '2026-09-03',
          completedPomodoros: 0,
          focusMinutes: 18,
          rankingMinutes: 18,
          goal: 4,
          goalMet: false,
        }),
      ),
    )

    montar()
    await assentar()

    iniciar()
    await assentar()
    await avancar(20 * MINUTE)
    abandonar()
    await assentar()

    expect(abandonou).toBe(true)
    expect(screen.getByTestId('today-summary')).toHaveTextContent('18 min')
  })

  it('deve mostrar o zero do servidor, quando o parcial não chegou aos 5 minutos', async () => {
    logado({ completedPomodoros: 0, focusMinutes: 0 })
    let abandonou = false
    server.use(
      http.post('/api/sessions/start', () =>
        HttpResponse.json({
          sessionId: SESSAO,
          startedAt: SERVIDOR_AGORA,
          endsAt: '2026-09-03T12:25:00.000Z',
          serverNow: SERVIDOR_AGORA,
        }),
      ),
      http.post(`/api/sessions/${SESSAO}/abandon`, () => {
        abandonou = true
        return HttpResponse.json({ countedMinutes: 0, discarded: true })
      }),
    )

    montar()
    await assentar()

    iniciar()
    await assentar()
    await avancar(3 * MINUTE)
    abandonar()
    await assentar()

    expect(abandonou).toBe(true)
    expect(screen.getByTestId('today-summary')).toHaveTextContent('0 min')
  })
})
