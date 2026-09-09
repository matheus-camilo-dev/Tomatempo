import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { HttpResponse, http } from 'msw'
import { describe, expect, it } from 'vitest'
import { rememberSession } from '../lib/sessionHint'
import { accountFixture } from '../test/fixtures'
import { pt } from '../test/i18nText'
import { renderRoute } from '../test/renderRoute'
import { server } from '../test/server'

const incompleta = accountFixture({ onboardingRequired: true, nickname: null, goal: null })

describe('onboarding', () => {
  it('não deve deixar sair da tela enquanto a conta estiver incompleta', async () => {
    rememberSession(localStorage)
    server.use(http.get('/api/me', () => HttpResponse.json(incompleta)))

    const { router } = renderRoute({ path: '/conta' })

    await waitFor(() => expect(router.state.location.pathname).toBe('/onboarding'))
    // `findBy` e não `getBy`: o roteador troca a rota antes do React repintar,
    // então logo depois do `waitFor` a tela ainda está vazia por um instante.
    expect(await screen.findByRole('heading', { name: pt.account.onboardingTitle })).toBeInTheDocument()
  })

  it('deve propor o fuso do navegador e deixar trocar antes de confirmar', async () => {
    rememberSession(localStorage)
    server.use(http.get('/api/me', () => HttpResponse.json(incompleta)))

    renderRoute({ path: '/onboarding' })

    const fuso = await screen.findByLabelText(pt.account.timeZoneLabel)
    expect(fuso).toHaveValue(Intl.DateTimeFormat().resolvedOptions().timeZone)

    await userEvent.setup().clear(fuso)
    await userEvent.setup().type(fuso, 'Europe/Lisbon')
    expect(fuso).toHaveValue('Europe/Lisbon')
  })

  it('deve completar a conta e liberar o timer', async () => {
    rememberSession(localStorage)
    let enviado: unknown
    let completou = false

    server.use(
      http.get('/api/me', () =>
        HttpResponse.json(completou ? accountFixture() : incompleta),
      ),
      http.post('/api/auth/onboarding', async ({ request }) => {
        enviado = await request.json()
        completou = true
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { router } = renderRoute({ path: '/onboarding' })
    const user = userEvent.setup()

    await user.type(await screen.findByLabelText(pt.account.nicknameLabel), 'joana')
    await user.selectOptions(screen.getByLabelText(pt.account.goalLabel), '8')
    await user.click(screen.getByRole('button', { name: pt.account.onboardingSubmit }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
    expect(enviado).toMatchObject({ nickname: 'joana', goal: 8, locale: 'pt-BR' })
  })

  it('deve traduzir o apelido já tomado pelo code', async () => {
    rememberSession(localStorage)
    server.use(
      http.get('/api/me', () => HttpResponse.json(incompleta)),
      http.post('/api/auth/onboarding', () =>
        HttpResponse.json(
          { title: 'Apelido já está em uso.', status: 409, code: 'nickname_taken' },
          { status: 409 },
        ),
      ),
    )

    renderRoute({ path: '/onboarding' })
    const user = userEvent.setup()

    await user.type(await screen.findByLabelText(pt.account.nicknameLabel), 'joana')
    await user.click(screen.getByRole('button', { name: pt.account.onboardingSubmit }))

    expect(await screen.findByRole('alert')).toHaveTextContent(pt.error.nickname_taken)
  })
})
