import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { HttpResponse, http } from 'msw'
import { describe, expect, it } from 'vitest'
import { accountFixture } from '../test/fixtures'
import { pt } from '../test/i18nText'
import { renderRoute } from '../test/renderRoute'
import { server } from '../test/server'

async function entrar() {
  const user = userEvent.setup()

  await user.type(screen.getByLabelText(pt.account.emailLabel), 'pessoa@exemplo.test')
  await user.type(screen.getByLabelText(pt.account.passwordLabel), 'senha-bem-longa')
  await user.click(screen.getByRole('button', { name: pt.account.signInSubmit }))
}

describe('login', () => {
  it('deve autenticar e cair no timer', async () => {
    server.use(
      http.post('/api/auth/login', () => new HttpResponse(null, { status: 204 })),
      http.get('/api/me', () => HttpResponse.json(accountFixture())),
    )

    const { router } = renderRoute({ path: '/entrar' })
    await entrar()

    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
  })

  it('deve traduzir a credencial inválida pelo code', async () => {
    server.use(
      http.post('/api/auth/login', () =>
        HttpResponse.json(
          { title: 'Credenciais inválidas.', status: 401, code: 'invalid_credentials' },
          { status: 401 },
        ),
      ),
    )

    renderRoute({ path: '/entrar' })
    await entrar()

    expect(await screen.findByRole('alert')).toHaveTextContent(pt.error.invalid_credentials)
    expect(screen.queryByText(/Credenciais inválidas\./)).not.toBeInTheDocument()
  })

  it('deve oferecer a entrada pelo Google', () => {
    renderRoute({ path: '/entrar' })

    expect(screen.getByRole('link', { name: pt.account.signInWithGoogle })).toHaveAttribute(
      'href',
      '/api/auth/google',
    )
  })

  // A volta do Google não passa pelo cliente: o servidor redireciona para cá
  // com o motivo na URL, e é a única pista que a pessoa recebe.
  it('deve traduzir o erro que o Google devolveu na URL', () => {
    renderRoute({ path: '/entrar?erro=external_email_not_verified' })

    expect(screen.getByRole('alert')).toHaveTextContent(pt.error.external_email_not_verified)
  })

  it('deve cair na mensagem genérica, quando o erro da URL é desconhecido', () => {
    renderRoute({ path: '/entrar?erro=coisa_que_nao_existe' })

    expect(screen.getByRole('alert')).toHaveTextContent(pt.error.unknown)
  })

  it('deve levar à recuperação de senha', () => {
    renderRoute({ path: '/entrar' })

    expect(screen.getByRole('link', { name: pt.account.forgotLink })).toHaveAttribute(
      'href',
      '/esqueci-senha',
    )
  })
})
