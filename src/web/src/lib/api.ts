/**
 * Único caminho do front até a API. Em produção e em desenvolvimento o SPA e a
 * API ficam na mesma origem — nginx e o proxy do Vite cuidam disso (§1.1) —,
 * então o caminho é relativo e o cookie de sessão viaja sozinho.
 */
export const API_BASE = '/api'

/** Erro com o `code` estável do problem+json; a tela traduz por ele, nunca pela mensagem (§4). */
export class ApiError extends Error {
  readonly status: number
  readonly code: string

  constructor(status: number, code: string) {
    super(`API ${status} ${code}`)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

/** `headers` fica de fora: quem os monta é o cliente, e ninguém aqui precisa acrescentar outro. */
export interface ApiRequestInit extends Omit<RequestInit, 'body' | 'headers'> {
  /** Corpo em JSON. Serializa e marca o `content-type` — o de formulário a API recusa (§7.2/A05). */
  readonly json?: unknown
}

export async function apiFetch<T = undefined>(path: string, init: ApiRequestInit = {}): Promise<T> {
  const { json, ...resto } = init

  const opcoes: RequestInit = {
    ...resto,
    // O identificador do usuário nunca sai daqui: quem diz quem é o dono da
    // requisição é o cookie, sempre (§7.2/A01).
    credentials: 'include',
  }

  if (json !== undefined) {
    opcoes.headers = { 'content-type': 'application/json' }
    opcoes.body = JSON.stringify(json)
  }

  let response: Response
  try {
    response = await fetch(`${API_BASE}${path}`, opcoes)
  } catch {
    // Offline é estado normal deste produto (§3.4), não exceção.
    throw new ApiError(0, 'network')
  }

  if (!response.ok) throw new ApiError(response.status, await codeDe(response))

  return (await corpoDe(response)) as T
}

async function corpoDe(response: Response): Promise<unknown> {
  const tipo = response.headers.get('content-type') ?? ''
  if (!tipo.includes('json')) return undefined

  return response.json()
}

/**
 * O `code` do problem+json. Quando a resposta nem é JSON — proxy fora do ar
 * devolvendo HTML de 502 — a tela ainda precisa de um code para traduzir.
 */
async function codeDe(response: Response): Promise<string> {
  const corpo = await corpoDe(response).catch(() => undefined)
  const code = (corpo as { code?: unknown } | undefined)?.code

  return typeof code === 'string' ? code : 'internal_error'
}
