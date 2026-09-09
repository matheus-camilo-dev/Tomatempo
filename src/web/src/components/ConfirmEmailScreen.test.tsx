import { screen } from '@testing-library/react'
import { HttpResponse, http } from 'msw'
import { describe, expect, it } from 'vitest'
import { pt } from '../test/i18nText'
import { renderRoute } from '../test/renderRoute'
import { server } from '../test/server'

describe('confirmação de e-mail', () => {
  it('deve consumir o token da URL e confirmar', async () => {
    let enviado: unknown
    server.use(
      http.post('/api/auth/confirm-email', async ({ request }) => {
        enviado = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )

    renderRoute({ path: '/confirmar-email?token=abc123' })

    expect(await screen.findByText(pt.account.emailConfirmed)).toBeInTheDocument()
    expect(enviado).toEqual({ token: 'abc123' })
  })

  it('deve virar mensagem, quando o token já foi usado ou venceu', async () => {
    server.use(
      http.post('/api/auth/confirm-email', () =>
        HttpResponse.json(
          { title: 'Token inválido ou vencido.', status: 400, code: 'invalid_token' },
          { status: 400 },
        ),
      ),
    )

    renderRoute({ path: '/confirmar-email?token=vencido' })

    expect(await screen.findByRole('alert')).toHaveTextContent(pt.error.invalid_token)
  })

  // Link truncado por cliente de e-mail é o caso comum aqui; sem token não há
  // o que perguntar à API.
  it('não deve chamar a API, quando a URL veio sem token', async () => {
    renderRoute({ path: '/confirmar-email' })

    expect(await screen.findByRole('alert')).toHaveTextContent(pt.error.invalid_token)
  })
})
