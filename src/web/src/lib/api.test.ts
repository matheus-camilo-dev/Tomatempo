import { HttpResponse, http } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '../test/server'
import { ApiError, apiFetch } from './api'

describe('cliente da API', () => {
  it('deve devolver o corpo desserializado, quando a resposta traz um', async () => {
    server.use(http.get('/api/me', () => HttpResponse.json({ nickname: 'pessoa' })))

    await expect(apiFetch('/me')).resolves.toEqual({ nickname: 'pessoa' })
  })

  it('deve devolver indefinido, quando a resposta é 204', async () => {
    server.use(http.post('/api/auth/logout', () => new HttpResponse(null, { status: 204 })))

    await expect(apiFetch('/auth/logout', { method: 'POST' })).resolves.toBeUndefined()
  })

  it('deve mandar o corpo como JSON, quando recebe `json`', async () => {
    let recebido: unknown
    let tipo: string | null = null

    server.use(
      http.post('/api/auth/login', async ({ request }) => {
        tipo = request.headers.get('content-type')
        recebido = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )

    await apiFetch('/auth/login', { method: 'POST', json: { email: 'a@b.test' } })

    expect(tipo).toBe('application/json')
    expect(recebido).toEqual({ email: 'a@b.test' })
  })

  it('deve levantar ApiError com o `code` do problem+json', async () => {
    server.use(
      http.post('/api/auth/login', () =>
        HttpResponse.json(
          { title: 'Credenciais inválidas.', status: 401, code: 'invalid_credentials' },
          { status: 401, headers: { 'content-type': 'application/problem+json' } },
        ),
      ),
    )

    const erro = await apiFetch('/auth/login', { method: 'POST' }).catch((e: unknown) => e)

    expect(erro).toBeInstanceOf(ApiError)
    expect((erro as ApiError).status).toBe(401)
    expect((erro as ApiError).code).toBe('invalid_credentials')
  })

  // O proxy na frente da API pode devolver HTML — 502 do nginx, Vite fora do ar.
  // Não é caso impossível, é o dia em que a API cai (§6.8).
  it('deve levantar ApiError, quando o corpo do erro nem é JSON', async () => {
    server.use(
      http.get('/api/me', () =>
        new HttpResponse('<html>502 Bad Gateway</html>', {
          status: 502,
          headers: { 'content-type': 'text/html' },
        }),
      ),
    )

    const erro = await apiFetch('/me').catch((e: unknown) => e)

    expect(erro).toBeInstanceOf(ApiError)
    expect((erro as ApiError).status).toBe(502)
    expect((erro as ApiError).code).toBe('internal_error')
  })

  // Offline é estado normal deste produto, não exceção (§3.4).
  it('deve levantar ApiError de rede, quando a chamada nem sai', async () => {
    server.use(http.get('/api/me', () => HttpResponse.error()))

    const erro = await apiFetch('/me').catch((e: unknown) => e)

    expect(erro).toBeInstanceOf(ApiError)
    expect((erro as ApiError).code).toBe('network')
  })

  it('não deve mandar identificador de usuário algum — quem decide é o cookie', async () => {
    let url = ''
    let cabecalhos: string[] = []

    server.use(
      http.get('/api/me', ({ request }) => {
        url = request.url
        cabecalhos = [...request.headers.keys()]
        return HttpResponse.json({})
      }),
    )

    await apiFetch('/me')

    expect(new URL(url).search).toBe('')
    expect(cabecalhos.some((nome) => /user|nickname|email|subject/i.test(nome))).toBe(false)
  })
})
