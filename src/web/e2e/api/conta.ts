import { randomUUID } from 'node:crypto'
import { expect, type Page } from '@playwright/test'

/**
 * Peças comuns do E2E da fase 2. Nada aqui afirma regra — só monta o cenário.
 */

export interface Conta {
  readonly email: string
  readonly senha: string
  readonly apelido: string
}

/**
 * Conta nova a cada caso: o Postgres do E2E sobrevive entre execuções, e
 * reaproveitar endereço faria o segundo caso cair no "e-mail já tem conta",
 * que é outro cenário (§3.10).
 */
export function novaConta(): Conta {
  const id = randomUUID().replaceAll('-', '').slice(0, 12)

  return {
    email: `e2e-${id}@exemplo.test`,
    senha: 'uma frase longa de teste',
    apelido: `e2e${id}`,
  }
}

/** Cadastra pela tela e volta com a pessoa logada no timer. */
export async function cadastrar(page: Page, conta: Conta = novaConta()): Promise<Conta> {
  await page.goto('/cadastrar')
  await page.locator('#cadastro-email').fill(conta.email)
  await page.locator('#cadastro-senha').fill(conta.senha)
  await page.locator('#cadastro-apelido').fill(conta.apelido)
  await page.getByRole('button', { name: 'Criar conta' }).click()

  await expect(page.getByRole('timer')).toBeVisible()
  await esperarODiaDaConta(page)

  return conta
}

export async function entrar(page: Page, conta: Conta): Promise<void> {
  await page.goto('/entrar')
  await page.locator('#login-email').fill(conta.email)
  await page.locator('#login-senha').fill(conta.senha)
  await page.getByRole('button', { name: 'Entrar' }).click()

  await expect(page.getByRole('timer')).toBeVisible()
  await esperarODiaDaConta(page)
}

/**
 * O placar de quem está logado vem do servidor, e a tela fica em branco até ele
 * chegar (REGRAS §2). Esperar por ele é o que evita ler o vazio como zero.
 */
export async function esperarODiaDaConta(page: Page): Promise<void> {
  await expect(page.getByTestId('today-summary')).toContainText(/pomodoros?/, { timeout: 15_000 })
}

/**
 * Começa o foco e só volta quando o servidor respondeu.
 *
 * Esperar o botão "Abandonar" não serve: o ciclo começa pelo relógio local
 * mesmo sem servidor (§3.4), então a tela muda antes de a sessão existir. Todo
 * caso que depende da sessão estar de pé — recuperar, abandonar no servidor,
 * ser assumido por outra aba — precisa desta espera, e não daquela.
 */
export async function iniciarFocoNoServidor(page: Page): Promise<void> {
  await Promise.all([
    page.waitForResponse(
      (r) => r.url().includes('/api/sessions/start') && r.request().method() === 'POST',
    ),
    iniciarFoco(page).click(),
  ])

  await expect(abandonar(page)).toBeVisible()
}

export const oDia = (page: Page) => page.getByTestId('today-summary')
export const iniciarFoco = (page: Page) => page.getByRole('button', { name: 'Iniciar foco' })
export const abandonar = (page: Page) => page.getByRole('button', { name: 'Abandonar' })
