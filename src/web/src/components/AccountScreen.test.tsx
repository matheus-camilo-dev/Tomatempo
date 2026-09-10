import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { HttpResponse, http } from 'msw'
import { describe, expect, it } from 'vitest'
import { hasSessionHint, rememberSession } from '../lib/sessionHint'
import { accountFixture, freezeLedgerFixture } from '../test/fixtures'
import { pt } from '../test/i18nText'
import { renderRoute } from '../test/renderRoute'
import { server } from '../test/server'

function contaEmPe(conta = accountFixture(), ledger = freezeLedgerFixture()) {
  rememberSession(localStorage)
  server.use(
    http.get('/api/me', () => HttpResponse.json(conta)),
    http.get('/api/me/freezes', () => HttpResponse.json(ledger)),
  )
}

describe('tela da conta', () => {
  it('deve mostrar a meta corrente', async () => {
    contaEmPe()

    renderRoute({ path: '/conta' })

    const meta = await screen.findByTestId('goal-current')
    expect(meta).toHaveTextContent('4')
  })

  it('deve mostrar a data em que a meta editada passa a valer', async () => {
    contaEmPe(
      accountFixture({
        goal: { current: 4, upcoming: 6, upcomingEffectiveFrom: '2026-09-10' },
      }),
    )

    renderRoute({ path: '/conta' })

    expect(await screen.findByTestId('goal-upcoming')).toHaveTextContent(/6.*10\/09\/2026/)
  })

  it('deve anunciar a data da nova meta logo depois de salvar', async () => {
    contaEmPe()
    let enviado: unknown
    server.use(
      http.put('/api/me/goal', async ({ request }) => {
        enviado = await request.json()
        return HttpResponse.json({ pomodoros: 8, effectiveFrom: '2026-09-10' })
      }),
    )

    renderRoute({ path: '/conta' })
    const user = userEvent.setup()

    await user.selectOptions(await screen.findByLabelText(pt.account.goalLabel), '8')
    await user.click(screen.getByRole('button', { name: pt.account.saveGoal }))

    expect(await screen.findByTestId('goal-upcoming')).toHaveTextContent(/8.*10\/09\/2026/)
    expect(enviado).toEqual({ pomodoros: 8 })
  })

  it('deve mostrar saldo e extrato vindos de /me/freezes', async () => {
    contaEmPe(
      accountFixture(),
      freezeLedgerFixture({
        balance: 3,
        entries: [
          { delta: 2, reason: 'Signup', refDate: null, createdAt: '2026-09-01T12:00:00Z' },
          { delta: 1, reason: 'StreakMilestone', refDate: '2026-09-08', createdAt: '2026-09-08T03:00:00Z' },
        ],
      }),
    )

    renderRoute({ path: '/conta' })

    expect(await screen.findByTestId('freezes-balance')).toHaveTextContent('3')
    const extrato = within(screen.getByTestId('freezes-ledger')).getAllByRole('listitem')
    expect(extrato).toHaveLength(2)
    expect(extrato[0]).toHaveTextContent(pt.freeze.reason.Signup)
    expect(extrato[1]).toHaveTextContent(pt.freeze.reason.StreakMilestone)
  })

  it('deve sair e voltar para a tela pública', async () => {
    contaEmPe()
    server.use(http.post('/api/auth/logout', () => new HttpResponse(null, { status: 204 })))

    const { router } = renderRoute({ path: '/conta' })
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: pt.account.signOut }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
    expect(hasSessionHint(localStorage)).toBe(false)
  })

  it('deve pedir a senha para excluir, quando a conta tem uma', async () => {
    contaEmPe()
    let enviado: unknown
    server.use(
      http.delete('/api/me', async ({ request }) => {
        enviado = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { router } = renderRoute({ path: '/conta' })
    const user = userEvent.setup()

    await user.type(
      await screen.findByLabelText(pt.account.deletePasswordLabel),
      'senha-bem-longa',
    )
    await user.click(screen.getByRole('button', { name: pt.account.deleteSubmit }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
    expect(enviado).toEqual({ password: 'senha-bem-longa' })
    expect(hasSessionHint(localStorage)).toBe(false)
  })

  it('não deve pedir senha para excluir, quando a conta veio do Google', async () => {
    contaEmPe(accountFixture({ hasPassword: false }))
    let enviado: unknown
    server.use(
      http.delete('/api/me', async ({ request }) => {
        enviado = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { router } = renderRoute({ path: '/conta' })
    const user = userEvent.setup()

    await waitFor(() => expect(screen.getByRole('button', { name: pt.account.deleteSubmit })).toBeEnabled())
    expect(screen.queryByLabelText(pt.account.deletePasswordLabel)).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: pt.account.deleteSubmit }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
    expect(enviado).toEqual({ password: null })
  })

  it('deve traduzir a senha errada na exclusão', async () => {
    contaEmPe()
    server.use(
      http.delete('/api/me', () =>
        HttpResponse.json(
          { title: 'Credenciais inválidas.', status: 401, code: 'invalid_credentials' },
          { status: 401 },
        ),
      ),
    )

    renderRoute({ path: '/conta' })
    const user = userEvent.setup()

    await user.type(await screen.findByLabelText(pt.account.deletePasswordLabel), 'errada12')
    await user.click(screen.getByRole('button', { name: pt.account.deleteSubmit }))

    expect(await screen.findByRole('alert')).toHaveTextContent(pt.error.invalid_credentials)
  })

  // O 401 que derruba a sessão local é o de `unauthenticated`, e não o status.
  // Errar a senha na exclusão também responde 401 — e quem errou a senha
  // continua logado, senão a tela puniria o engano com um logout (§5).
  it('não deve deslogar quem errou a senha na exclusão', async () => {
    contaEmPe()
    server.use(
      http.delete('/api/me', () =>
        HttpResponse.json(
          { title: 'Credenciais inválidas.', status: 401, code: 'invalid_credentials' },
          { status: 401 },
        ),
      ),
    )

    const { router } = renderRoute({ path: '/conta' })
    const user = userEvent.setup()

    await user.type(await screen.findByLabelText(pt.account.deletePasswordLabel), 'errada12')
    await user.click(screen.getByRole('button', { name: pt.account.deleteSubmit }))

    await screen.findByRole('alert')
    expect(hasSessionHint(localStorage)).toBe(true)
    expect(router.state.location.pathname).toBe('/conta')
    expect(await screen.findByTestId('goal-current')).toBeInTheDocument()
  })

  it('deve mandar para o login, quando não há sessão alguma', async () => {
    const { router } = renderRoute({ path: '/conta' })

    await waitFor(() => expect(router.state.location.pathname).toBe('/entrar'))
  })
})
