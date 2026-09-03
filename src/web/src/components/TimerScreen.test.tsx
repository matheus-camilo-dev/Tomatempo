import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { act } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createTimerStore, type TimerStore } from '../hooks/useTimerStore'
import { TimerScreen } from './TimerScreen'

const MINUTE = 60_000
const START = new Date(2026, 8, 2, 10, 0, 0).getTime()

function memoryStorage(): Storage {
  const data = new Map<string, string>()

  return {
    get length() {
      return data.size
    },
    clear: () => data.clear(),
    getItem: (key: string) => data.get(key) ?? null,
    key: (index: number) => [...data.keys()][index] ?? null,
    removeItem: (key: string) => void data.delete(key),
    setItem: (key: string, value: string) => void data.set(key, value),
  }
}

function setup(): { store: TimerStore; user: ReturnType<typeof userEvent.setup> } {
  const store = createTimerStore({ storage: memoryStorage() })
  const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime, delay: null })
  render(<TimerScreen store={store} />)

  return { store, user }
}

/** Avança o relógio deixando o intervalo do timer rodar. */
function advance(ms: number): void {
  act(() => void vi.advanceTimersByTime(ms))
}

beforeEach(() => {
  vi.useFakeTimers()
  vi.setSystemTime(START)
})

afterEach(() => {
  vi.useRealTimers()
})

describe('estado inicial', () => {
  it('deve mostrar 25 minutos de foco prontos para começar', () => {
    setup()

    expect(screen.getByRole('timer')).toHaveTextContent('25:00')
    expect(screen.getByRole('status')).toHaveTextContent(/foco/i)
    expect(screen.getByRole('button', { name: /iniciar/i })).toBeInTheDocument()
  })

  it('deve mostrar o dia zerado', () => {
    setup()

    expect(screen.getByTestId('today-summary')).toHaveTextContent(/0 pomodoros/i)
  })

  it('deve convidar a criar conta (CTA de deslogado)', () => {
    setup()

    expect(screen.getByTestId('signup-cta')).toHaveTextContent(/ranking/i)
  })
})

describe('ciclo de foco', () => {
  it('deve contar o tempo depois de iniciar', async () => {
    const { user } = setup()

    await user.click(screen.getByRole('button', { name: /iniciar/i }))
    advance(60_000)

    expect(screen.getByRole('timer')).toHaveTextContent('24:00')
  })

  it('deve oferecer abandonar enquanto corre', async () => {
    const { user } = setup()

    await user.click(screen.getByRole('button', { name: /iniciar/i }))

    expect(screen.getByRole('button', { name: /abandonar/i })).toBeInTheDocument()
  })

  it('deve encerrar ao fim e esperar o usuário, sem emendar a pausa', async () => {
    const { user } = setup()

    await user.click(screen.getByRole('button', { name: /iniciar/i }))
    advance(25 * MINUTE)

    expect(screen.getByRole('timer')).toHaveTextContent('00:00')
    expect(screen.getByRole('status')).toHaveTextContent(/conclu/i)
    expect(screen.getByRole('button', { name: /pausa curta/i })).toBeInTheDocument()
  })

  it('deve somar o pomodoro concluído ao dia', async () => {
    const { user } = setup()

    await user.click(screen.getByRole('button', { name: /iniciar/i }))
    advance(25 * MINUTE)

    expect(screen.getByTestId('today-summary')).toHaveTextContent(/1 pomodoro/i)
    expect(screen.getByTestId('today-summary')).toHaveTextContent(/25 min/i)
  })

  it('deve iniciar a pausa somente após o clique do usuário', async () => {
    const { user } = setup()
    await user.click(screen.getByRole('button', { name: /iniciar/i }))
    advance(25 * MINUTE)

    await user.click(screen.getByRole('button', { name: /pausa curta/i }))

    expect(screen.getByRole('status')).toHaveTextContent(/pausa curta/i)
    expect(screen.getByRole('timer')).toHaveTextContent('05:00')
  })
})

describe('abandono', () => {
  it('deve creditar o parcial e voltar ao início', async () => {
    const { user } = setup()
    await user.click(screen.getByRole('button', { name: /iniciar/i }))
    advance(20 * MINUTE)

    await user.click(screen.getByRole('button', { name: /abandonar/i }))

    expect(screen.getByTestId('today-summary')).toHaveTextContent(/20 min/i)
    expect(screen.getByTestId('today-summary')).toHaveTextContent(/0 pomodoros/i)
    expect(screen.getByRole('button', { name: /iniciar/i })).toBeInTheDocument()
  })

  it('deve descartar parcial abaixo de 5 minutos', async () => {
    const { user } = setup()
    await user.click(screen.getByRole('button', { name: /iniciar/i }))
    advance(3 * MINUTE)

    await user.click(screen.getByRole('button', { name: /abandonar/i }))

    expect(screen.getByTestId('today-summary')).toHaveTextContent(/0 min/i)
  })
})

describe('pausa longa', () => {
  it('deve oferecer pausa longa depois do quarto pomodoro', async () => {
    const { user } = setup()

    // Três ciclos completos de foco + pausa curta.
    for (let cycle = 1; cycle <= 3; cycle++) {
      await user.click(screen.getByRole('button', { name: /iniciar/i }))
      advance(25 * MINUTE)
      await user.click(screen.getByRole('button', { name: /pausa curta/i }))
      advance(5 * MINUTE)
    }

    // Quarto foco.
    await user.click(screen.getByRole('button', { name: /iniciar foco/i }))
    advance(25 * MINUTE)

    expect(screen.getByRole('button', { name: /pausa longa/i })).toBeInTheDocument()
    expect(screen.getByTestId('today-summary')).toHaveTextContent(/4 pomodoros/i)
  })
})

describe('acessibilidade', () => {
  it('deve anunciar a mudança de ciclo em região viva', () => {
    setup()

    expect(screen.getByRole('status')).toHaveAttribute('aria-live', 'polite')
  })

  it('deve dar rótulo legível ao contador', () => {
    setup()

    expect(screen.getByRole('timer')).toHaveAccessibleName(/restante/i)
  })
})
