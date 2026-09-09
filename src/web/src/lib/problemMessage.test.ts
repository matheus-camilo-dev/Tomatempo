import { describe, expect, it } from 'vitest'
import { problemMessageKey } from './problemMessage'

describe('mensagem de erro da API', () => {
  it('deve mapear o code conhecido para a chave do catálogo', () => {
    expect(problemMessageKey('invalid_credentials')).toBe('error.invalid_credentials')
  })

  // Sem isto, um code novo do servidor viraria chave inexistente e o i18next
  // renderizaria a própria chave crua na tela.
  it('deve cair na mensagem genérica, quando o code é desconhecido', () => {
    expect(problemMessageKey('code_que_o_front_nao_conhece')).toBe('error.unknown')
  })

  it('deve cair na mensagem genérica, quando não veio code algum', () => {
    expect(problemMessageKey(undefined)).toBe('error.unknown')
  })
})
