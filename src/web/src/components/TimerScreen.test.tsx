import { fireEvent, render, screen } from '@testing-library/react'
import { act } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { AlertPorts } from '../lib/alerts'
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

/**
 * `fireEvent` e não `user-event`: com os fake timers instalados, a promise de
 * `user.click` nunca resolve (§6.4). Para clique em botão a diferença entre os
 * dois é irrelevante — nenhuma regra daqui depende da sequência de pointer.
 */
function setup(): TimerStore {
  const store = createTimerStore({ storage: memoryStorage() })
  render(<TimerScreen store={store} />)

  return store
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
  it('deve contar o tempo depois de iniciar', () => {
    setup()

    fireEvent.click(screen.getByRole('button', { name: /iniciar/i }))
    advance(60_000)

    expect(screen.getByRole('timer')).toHaveTextContent('24:00')
  })

  it('deve oferecer abandonar enquanto corre', () => {
    setup()

    fireEvent.click(screen.getByRole('button', { name: /iniciar/i }))

    expect(screen.getByRole('button', { name: /abandonar/i })).toBeInTheDocument()
  })

  it('deve encerrar ao fim e esperar o usuário, sem emendar a pausa', () => {
    setup()

    fireEvent.click(screen.getByRole('button', { name: /iniciar/i }))
    advance(25 * MINUTE)

    expect(screen.getByRole('timer')).toHaveTextContent('00:00')
    expect(screen.getByRole('status')).toHaveTextContent(/conclu/i)
    expect(screen.getByRole('button', { name: /pausa curta/i })).toBeInTheDocument()
  })

  it('deve somar o pomodoro concluído ao dia', () => {
    setup()

    fireEvent.click(screen.getByRole('button', { name: /iniciar/i }))
    advance(25 * MINUTE)

    expect(screen.getByTestId('today-summary')).toHaveTextContent(/1 pomodoro/i)
    expect(screen.getByTestId('today-summary')).toHaveTextContent(/25 min/i)
  })

  it('deve iniciar a pausa somente após o clique do usuário', () => {
    setup()
    fireEvent.click(screen.getByRole('button', { name: /iniciar/i }))
    advance(25 * MINUTE)

    fireEvent.click(screen.getByRole('button', { name: /pausa curta/i }))

    expect(screen.getByRole('status')).toHaveTextContent(/pausa curta/i)
    expect(screen.getByRole('timer')).toHaveTextContent('05:00')
  })
})

describe('abandono', () => {
  it('deve creditar o parcial e voltar ao início', () => {
    setup()
    fireEvent.click(screen.getByRole('button', { name: /iniciar/i }))
    advance(20 * MINUTE)

    fireEvent.click(screen.getByRole('button', { name: /abandonar/i }))

    expect(screen.getByTestId('today-summary')).toHaveTextContent(/20 min/i)
    expect(screen.getByTestId('today-summary')).toHaveTextContent(/0 pomodoros/i)
    expect(screen.getByRole('button', { name: /iniciar/i })).toBeInTheDocument()
  })

  it('deve descartar parcial abaixo de 5 minutos', () => {
    setup()
    fireEvent.click(screen.getByRole('button', { name: /iniciar/i }))
    advance(3 * MINUTE)

    fireEvent.click(screen.getByRole('button', { name: /abandonar/i }))

    expect(screen.getByTestId('today-summary')).toHaveTextContent(/0 min/i)
  })
})

describe('pausa longa', () => {
  it('deve oferecer pausa longa depois do quarto pomodoro', () => {
    setup()

    // Três ciclos completos de foco + pausa curta.
    for (let cycle = 1; cycle <= 3; cycle++) {
      fireEvent.click(screen.getByRole('button', { name: /iniciar/i }))
      advance(25 * MINUTE)
      fireEvent.click(screen.getByRole('button', { name: /pausa curta/i }))
      advance(5 * MINUTE)
    }

    // Quarto foco.
    fireEvent.click(screen.getByRole('button', { name: /iniciar foco/i }))
    advance(25 * MINUTE)

    expect(screen.getByRole('button', { name: /pausa longa/i })).toBeInTheDocument()
    expect(screen.getByTestId('today-summary')).toHaveTextContent(/4 pomodoros/i)
  })
})

describe('alertas', () => {
  function setupWithAlerts(): AlertPorts {
    const alerts: AlertPorts = {
      prepare: vi.fn(),
      playAlarm: vi.fn(),
      notify: vi.fn(),
      isHidden: () => false,
    }
    render(<TimerScreen store={createTimerStore({ storage: memoryStorage() })} alerts={alerts} />)

    return alerts
  }

  it('deve destravar áudio e permissão no gesto de iniciar', () => {
    const alerts = setupWithAlerts()

    fireEvent.click(screen.getByRole('button', { name: /iniciar/i }))

    expect(alerts.prepare).toHaveBeenCalledTimes(1)
  })

  it('deve tocar o alarme quando o ciclo termina', () => {
    const alerts = setupWithAlerts()

    fireEvent.click(screen.getByRole('button', { name: /iniciar/i }))
    advance(25 * MINUTE)

    expect(alerts.playAlarm).toHaveBeenCalledTimes(1)
  })
})

describe('idioma', () => {
  it('deve oferecer a troca de idioma', () => {
    setup()

    expect(screen.getByRole('button', { name: /english/i })).toBeInTheDocument()
  })

  it('deve traduzir a tela ao escolher inglês', async () => {
    setup()

    fireEvent.click(screen.getByRole('button', { name: /english/i }))
    // `findBy*` usa waitFor, que não avança sob fake timers; um act assíncrono
    // deixa a troca de idioma assentar sem depender do relógio.
    await act(async () => {})

    expect(screen.getByRole('button', { name: /start focus/i })).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent(/focus/i)
    expect(screen.getByTestId('today-summary')).toHaveTextContent(/focused today/i)
  })

  it('deve marcar o idioma corrente para leitores de tela', () => {
    setup()

    expect(screen.getByRole('button', { name: /português/i })).toHaveAttribute(
      'aria-pressed',
      'true',
    )
  })
})

describe('tema', () => {
  it('deve oferecer as três opções de tema', () => {
    setup()

    expect(screen.getByRole('button', { name: /sistema/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^claro$/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^escuro$/i })).toBeInTheDocument()
  })

  it('deve marcar "sistema" como corrente por padrão', () => {
    setup()

    expect(screen.getByRole('button', { name: /sistema/i })).toHaveAttribute(
      'aria-pressed',
      'true',
    )
  })

  it('deve aplicar o tema escuro ao escolhê-lo', () => {
    setup()

    fireEvent.click(screen.getByRole('button', { name: /^escuro$/i }))

    expect(document.documentElement.classList.contains('dark')).toBe(true)
    expect(screen.getByRole('button', { name: /^escuro$/i })).toHaveAttribute(
      'aria-pressed',
      'true',
    )
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
