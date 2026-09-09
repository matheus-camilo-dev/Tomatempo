import { describe, expect, it } from 'vitest'
import { memoryStorage } from '../test/memoryStorage'
import { SESSION_HINT_KEY, forgetSession, hasSessionHint, rememberSession } from './sessionHint'

describe('pista de sessão', () => {
  it('deve começar apagada — visitante anônimo não tem conta a reidratar', () => {
    expect(hasSessionHint(memoryStorage())).toBe(false)
  })

  it('deve lembrar e esquecer', () => {
    const storage = memoryStorage()

    rememberSession(storage)
    expect(hasSessionHint(storage)).toBe(true)

    forgetSession(storage)
    expect(hasSessionHint(storage)).toBe(false)
  })

  it('não deve guardar identificador algum — só o fato de haver sessão', () => {
    const storage = memoryStorage()

    rememberSession(storage)

    expect(storage.length).toBe(1)
    expect(storage.key(0)).toBe(SESSION_HINT_KEY)
    expect(storage.getItem(SESSION_HINT_KEY)).toBe('1')
  })

  it('deve tolerar armazenamento ausente — o app continua servindo o timer', () => {
    expect(hasSessionHint(null)).toBe(false)
    expect(() => rememberSession(null)).not.toThrow()
    expect(() => forgetSession(null)).not.toThrow()
  })
})
