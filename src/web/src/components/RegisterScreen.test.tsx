import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { HttpResponse, http } from 'msw'
import { describe, expect, it } from 'vitest'
import { accountFixture } from '../test/fixtures'
import { pt } from '../test/i18nText'
import { renderRoute } from '../test/renderRoute'
import { server } from '../test/server'

async function preencher() {
  const user = userEvent.setup()

  await user.type(screen.getByLabelText(pt.account.emailLabel), 'nova@exemplo.test')
  await user.type(screen.getByLabelText(pt.account.passwordLabel), 'senha-bem-longa')
  await user.type(screen.getByLabelText(pt.account.nicknameLabel), 'pessoa')
  await user.selectOptions(screen.getByLabelText(pt.account.goalLabel), '6')

  return user
}

describe('cadastro', () => {
  it('deve entrar logado e cair no timer, quando o endereço é novo', async () => {
    let enviado: unknown
    server.use(
      http.post('/api/auth/register', async ({ request }) => {
        enviado = await request.json()
        return new HttpResponse(null, { status: 202 })
      }),
      http.get('/api/me', () => HttpResponse.json(accountFixture({ emailConfirmed: false }))),
    )

    const { router } = renderRoute({ path: '/cadastrar' })
    const user = await preencher()
    await user.click(screen.getByRole('button', { name: pt.account.signUpSubmit }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
    expect(enviado).toMatchObject({
      email: 'nova@exemplo.test',
      nickname: 'pessoa',
      goal: 6,
      locale: 'pt-BR',
    })
  })

  it('deve avisar do e-mail pendente de confirmação, já dentro do app', async () => {
    server.use(
      http.post('/api/auth/register', () => new HttpResponse(null, { status: 202 })),
      http.get('/api/me', () => HttpResponse.json(accountFixture({ emailConfirmed: false }))),
    )

    renderRoute({ path: '/cadastrar' })
    const user = await preencher()
    await user.click(screen.getByRole('button', { name: pt.account.signUpSubmit }))

    expect(await screen.findByText(pt.account.unconfirmedEmail)).toBeInTheDocument()
  })

  // A API responde 202 para endereço novo e para endereço que já tem conta; só
  // a sessão distingue os dois (§3.10). A tela não pode inventar a diferença.
  it('deve mostrar aviso neutro, quando o endereço já tinha conta', async () => {
    server.use(
      http.post('/api/auth/register', () => new HttpResponse(null, { status: 202 })),
      http.get('/api/me', () =>
        HttpResponse.json({ status: 401, code: 'unauthenticated' }, { status: 401 }),
      ),
    )

    const { router } = renderRoute({ path: '/cadastrar' })
    const user = await preencher()
    await user.click(screen.getByRole('button', { name: pt.account.signUpSubmit }))

    expect(await screen.findByText(pt.account.checkYourEmail)).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/cadastrar')
  })

  it('deve traduzir o erro pelo code, sem mostrar o texto cru do servidor', async () => {
    server.use(
      http.post('/api/auth/register', () =>
        HttpResponse.json(
          { title: 'Senha muito curta ou muito comum.', status: 400, code: 'weak_password' },
          { status: 400 },
        ),
      ),
    )

    renderRoute({ path: '/cadastrar' })
    const user = await preencher()
    await user.click(screen.getByRole('button', { name: pt.account.signUpSubmit }))

    expect(await screen.findByRole('alert')).toHaveTextContent(pt.error.weak_password)
    expect(screen.queryByText(/Senha muito curta ou muito comum\./)).not.toBeInTheDocument()
  })

  it('deve propor o fuso do navegador e deixar trocar antes de enviar', async () => {
    renderRoute({ path: '/cadastrar' })

    const fuso = screen.getByLabelText(pt.account.timeZoneLabel)
    expect(fuso).toHaveValue(Intl.DateTimeFormat().resolvedOptions().timeZone)
    expect(fuso).toBeEnabled()
  })
})
