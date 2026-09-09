import { QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import { HttpResponse, http } from 'msw'
import type { ReactNode } from 'react'
import { act } from 'react'
import { describe, expect, it } from 'vitest'
import { createQueryClient } from '../lib/queryClient'
import { hasSessionHint, rememberSession } from '../lib/sessionHint'
import { accountFixture } from '../test/fixtures'
import { server } from '../test/server'
import { useAccount, useSignOut } from './useAccount'

function wrapper({ children }: { readonly children: ReactNode }) {
  const client = createQueryClient()
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>
}

describe('estado da conta', () => {
  it('deve reidratar a conta por GET /me, quando a sessão já existia', async () => {
    rememberSession(localStorage)
    server.use(http.get('/api/me', () => HttpResponse.json(accountFixture({ nickname: 'joana' }))))

    const { result } = renderHook(() => useAccount(), { wrapper })

    await waitFor(() => expect(result.current.account?.nickname).toBe('joana'))
  })

  // Nenhum handler declarado neste caso: se o hook chamasse a API, o servidor
  // de teste estouraria o caso com "requisição não declarada".
  it('não deve chamar a API, quando nunca houve sessão nesta máquina', async () => {
    const { result } = renderHook(() => useAccount(), { wrapper })

    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(result.current.account).toBeNull()
  })

  it('deve derrubar o estado local, quando a API responde 401', async () => {
    rememberSession(localStorage)
    server.use(
      http.get('/api/me', () =>
        HttpResponse.json({ status: 401, code: 'unauthenticated' }, { status: 401 }),
      ),
    )

    const { result } = renderHook(() => useAccount(), { wrapper })

    await waitFor(() => expect(result.current.account).toBeNull())
    expect(hasSessionHint(localStorage)).toBe(false)
  })

  it('deve encerrar a sessão no servidor e limpar o estado local', async () => {
    rememberSession(localStorage)
    let chamou = false
    server.use(
      http.get('/api/me', () => HttpResponse.json(accountFixture())),
      http.post('/api/auth/logout', () => {
        chamou = true
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { result } = renderHook(() => ({ ...useAccount(), ...useSignOut() }), { wrapper })
    await waitFor(() => expect(result.current.account).not.toBeNull())

    await act(async () => void (await result.current.signOut()))

    expect(chamou).toBe(true)
    expect(result.current.account).toBeNull()
    expect(hasSessionHint(localStorage)).toBe(false)
  })

  // Sair com a API fora do ar não pode deixar a tela achando que ainda está
  // logada: o cookie pode já ter morrido, e insistir é pior que limpar.
  it('deve limpar o estado local mesmo quando o logout falha', async () => {
    rememberSession(localStorage)
    server.use(
      http.get('/api/me', () => HttpResponse.json(accountFixture())),
      http.post('/api/auth/logout', () => HttpResponse.error()),
    )

    const { result } = renderHook(() => ({ ...useAccount(), ...useSignOut() }), { wrapper })
    await waitFor(() => expect(result.current.account).not.toBeNull())

    await act(async () => void (await result.current.signOut()))

    expect(result.current.account).toBeNull()
    expect(hasSessionHint(localStorage)).toBe(false)
  })

  it('não deve guardar identificador de usuário no armazenamento local', async () => {
    rememberSession(localStorage)
    server.use(http.get('/api/me', () => HttpResponse.json(accountFixture())))

    const { result } = renderHook(() => useAccount(), { wrapper })
    await waitFor(() => expect(result.current.account).not.toBeNull())

    const guardado = Object.keys(localStorage).map((chave) => `${chave}=${localStorage.getItem(chave)}`)
    expect(guardado.join('|')).not.toMatch(/pessoa|exemplo\.test/i)
  })
})
