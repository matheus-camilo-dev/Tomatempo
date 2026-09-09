import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { HttpResponse, http } from 'msw'
import { describe, expect, it } from 'vitest'
import { pt } from '../test/i18nText'
import { renderRoute } from '../test/renderRoute'
import { server } from '../test/server'

describe('pedido de recuperação', () => {
  // A API responde 202 com ou sem conta no endereço (§3.10). A tela precisa
  // responder igual nos dois casos, ou desfaz no front o que a API protege.
  it('deve mostrar a mesma confirmação, tenha o endereço conta ou não', async () => {
    server.use(http.post('/api/auth/forgot-password', () => new HttpResponse(null, { status: 202 })))

    renderRoute({ path: '/esqueci-senha' })
    const user = userEvent.setup()

    await user.type(screen.getByLabelText(pt.account.emailLabel), 'quemsabe@exemplo.test')
    await user.click(screen.getByRole('button', { name: pt.account.forgotSubmit }))

    expect(await screen.findByText(pt.account.forgotRequested)).toBeInTheDocument()
  })
})

describe('troca de senha', () => {
  it('deve trocar a senha com o token da URL e levar ao login', async () => {
    let enviado: unknown
    server.use(
      http.post('/api/auth/reset-password', async ({ request }) => {
        enviado = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { router } = renderRoute({ path: '/nova-senha?token=abc123' })
    const user = userEvent.setup()

    await user.type(screen.getByLabelText(pt.account.newPasswordLabel), 'outra-senha-longa')
    await user.click(screen.getByRole('button', { name: pt.account.resetSubmit }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/entrar'))
    expect(enviado).toEqual({ token: 'abc123', password: 'outra-senha-longa' })
  })

  it('deve traduzir a senha fraca pelo code', async () => {
    server.use(
      http.post('/api/auth/reset-password', () =>
        HttpResponse.json(
          { title: 'Senha muito curta ou muito comum.', status: 400, code: 'weak_password' },
          { status: 400 },
        ),
      ),
    )

    renderRoute({ path: '/nova-senha?token=abc123' })
    const user = userEvent.setup()

    await user.type(screen.getByLabelText(pt.account.newPasswordLabel), 'senha123')
    await user.click(screen.getByRole('button', { name: pt.account.resetSubmit }))

    expect(await screen.findByRole('alert')).toHaveTextContent(pt.error.weak_password)
  })
})
