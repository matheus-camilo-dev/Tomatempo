import { type QueryClient, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback } from 'react'
import { ApiError, apiFetch } from '../lib/api'
import type { Account, FreezeLedger, GoalChange } from '../lib/contracts'
import { clearOfflineQueue } from '../lib/offlineQueue'
import { FREEZES_QUERY_KEY, ME_QUERY_KEY } from '../lib/queryClient'
import { forgetSession, rememberSession } from '../lib/sessionHint'
import { useSessionHint } from './useSessionHint'

export interface UseAccountResult {
  /** `null` quando não há sessão — não é erro, é o estado normal de quem chega. */
  readonly account: Account | null
  readonly isLoading: boolean
}

export function useAccount(): UseAccountResult {
  const temSessao = useSessionHint()

  const query = useQuery({
    queryKey: ME_QUERY_KEY,
    queryFn: () => apiFetch<Account>('/me'),
    // Sem pista de sessão o app não pergunta nada: é isso que deixa o visitante
    // anônimo usar o timer sem uma única chamada à API (§5).
    enabled: temSessao,
  })

  return {
    account: temSessao ? (query.data ?? null) : null,
    isLoading: temSessao && query.isPending,
  }
}

export interface SignUpInput {
  readonly email: string
  readonly password: string
  readonly nickname: string
  readonly goal: number
  readonly timeZone: string
  readonly locale: string
}

/**
 * Cadastro. A API responde 202 tanto para endereço novo quanto para endereço
 * que já tem conta, e só a sessão distingue os dois (§3.10) — por isso quem
 * descobre em qual caso estamos é o `GET /me` logo depois, e não a resposta.
 *
 * Resolve para a conta quando o cadastro criou sessão, e para `null` quando o
 * endereço já tinha dono; a tela mostra o mesmo aviso neutro nos dois casos.
 */
export function useSignUp() {
  const client = useQueryClient()

  return useMutation({
    mutationFn: async (input: SignUpInput) => {
      await apiFetch('/auth/register', { method: 'POST', json: input })
      await clearOfflineQueue()
      rememberSession()

      return carregarConta(client)
    },
  })
}

export interface SignInInput {
  readonly email: string
  readonly password: string
}

export function useSignIn() {
  const client = useQueryClient()

  return useMutation({
    mutationFn: async (input: SignInInput) => {
      await apiFetch('/auth/login', { method: 'POST', json: input })
      // Quem entra começa com a fila vazia: fechar a aba não passa por logout
      // nem por 401, e a fila de quem estava aqui não sabe de quem é (§3.4).
      await clearOfflineQueue()
      rememberSession()

      return carregarConta(client)
    },
  })
}

export function useSignOut() {
  const client = useQueryClient()

  const signOut = useCallback(async () => {
    try {
      await apiFetch('/auth/logout', { method: 'POST' })
    } catch {
      // Sair com a API fora do ar não pode deixar a tela achando que continua
      // logada: o cookie pode já ter morrido, e insistir é pior que limpar.
    }

    forgetSession()
    // A fila não sabe de quem ela é (§7.2/A01): sobrevivendo à saída, drenaria
    // na próxima conta que entrasse neste navegador (§3.4).
    await clearOfflineQueue()
    client.removeQueries({ queryKey: ME_QUERY_KEY })
  }, [client])

  return { signOut }
}

export interface OnboardingInput {
  readonly nickname: string
  readonly goal: number
  readonly timeZone: string
  readonly locale: string
}

export function useCompleteOnboarding() {
  const client = useQueryClient()

  return useMutation({
    mutationFn: async (input: OnboardingInput) => {
      await apiFetch('/auth/onboarding', { method: 'POST', json: input })

      return client.invalidateQueries({ queryKey: ME_QUERY_KEY })
    },
  })
}

export function useUpdateGoal() {
  return useMutation({
    mutationFn: (pomodoros: number) =>
      apiFetch<GoalChange>('/me/goal', { method: 'PUT', json: { pomodoros } }),
  })
}

export function useFreezes(enabled: boolean) {
  return useQuery({
    queryKey: FREEZES_QUERY_KEY,
    queryFn: () => apiFetch<FreezeLedger>('/me/freezes'),
    enabled,
  })
}

export function useDeleteAccount() {
  const client = useQueryClient()

  return useMutation({
    mutationFn: async (password: string | null) => {
      await apiFetch('/me', { method: 'DELETE', json: { password } })

      forgetSession()
      await clearOfflineQueue()
      client.removeQueries({ queryKey: ME_QUERY_KEY })
    },
  })
}

export function useResendConfirmation() {
  return useMutation({
    mutationFn: (email: string) =>
      apiFetch('/auth/resend-confirmation', { method: 'POST', json: { email } }),
  })
}

export function useForgotPassword() {
  return useMutation({
    mutationFn: (email: string) =>
      apiFetch('/auth/forgot-password', { method: 'POST', json: { email } }),
  })
}

export function useResetPassword() {
  return useMutation({
    mutationFn: (input: { readonly token: string; readonly password: string }) =>
      apiFetch('/auth/reset-password', { method: 'POST', json: input }),
  })
}

export function useConfirmEmail() {
  return useMutation({
    mutationFn: (token: string) =>
      apiFetch('/auth/confirm-email', { method: 'POST', json: { token } }),
  })
}

async function carregarConta(client: QueryClient): Promise<Account | null> {
  try {
    return await client.fetchQuery({
      queryKey: ME_QUERY_KEY,
      queryFn: () => apiFetch<Account>('/me'),
    })
  } catch (erro) {
    // Cadastro em endereço que já tinha conta não abre sessão. Não é falha: é a
    // resposta indistinguível que a API devolve de propósito (§3.10).
    if (erro instanceof ApiError && erro.code === 'unauthenticated') return null

    throw erro
  }
}
