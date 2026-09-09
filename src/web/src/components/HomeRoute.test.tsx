import { screen, waitFor, within } from '@testing-library/react'
import { HttpResponse, http } from 'msw'
import { describe, expect, it } from 'vitest'
import { rememberSession } from '../lib/sessionHint'
import { accountFixture } from '../test/fixtures'
import { pt } from '../test/i18nText'
import { renderRoute } from '../test/renderRoute'
import { server } from '../test/server'

describe('tela inicial', () => {
  // Nenhum handler é declarado aqui: qualquer chamada à API estoura o caso.
  it('deve servir o timer sem chamar a API, para quem nunca entrou', async () => {
    renderRoute({ path: '/' })

    expect(await screen.findByRole('timer')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: pt.nav.account })).not.toBeInTheDocument()
  })

  it('deve levar ao cadastro pelo CTA', () => {
    renderRoute({ path: '/' })

    const cta = within(screen.getByTestId('signup-cta')).getByRole('link')
    expect(cta).toHaveAttribute('href', '/cadastrar')
  })

  it('deve oferecer a conta, quando a sessão está de pé', async () => {
    rememberSession(localStorage)
    server.use(http.get('/api/me', () => HttpResponse.json(accountFixture())))

    renderRoute({ path: '/' })

    expect(await screen.findByRole('link', { name: pt.nav.account })).toHaveAttribute('href', '/conta')
    expect(screen.queryByTestId('signup-cta')).not.toBeInTheDocument()
  })

  it('deve avisar sobre o e-mail não confirmado e oferecer o reenvio', async () => {
    rememberSession(localStorage)
    server.use(
      http.get('/api/me', () => HttpResponse.json(accountFixture({ emailConfirmed: false }))),
    )

    renderRoute({ path: '/' })

    expect(await screen.findByText(pt.account.unconfirmedEmail)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: pt.account.resendConfirmation })).toBeInTheDocument()
  })

  it('deve mandar para o onboarding, quando a conta ainda não foi completada', async () => {
    rememberSession(localStorage)
    server.use(
      http.get('/api/me', () =>
        HttpResponse.json(accountFixture({ onboardingRequired: true, nickname: null, goal: null })),
      ),
    )

    const { router } = renderRoute({ path: '/' })

    await waitFor(() => expect(router.state.location.pathname).toBe('/onboarding'))
  })
})
