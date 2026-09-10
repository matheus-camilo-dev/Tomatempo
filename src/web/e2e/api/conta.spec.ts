import { expect, test } from '@playwright/test'
import { cadastrar, entrar, esperarODiaDaConta, iniciarFoco, novaConta, oDia } from './conta'

/**
 * Conta e sessão em navegador de verdade, contra a API e o Postgres (§6.3).
 *
 * O que a suíte unitária não alcança e estes casos alcançam: o cookie
 * `HttpOnly` sendo emitido, guardado e reenviado pelo navegador, e o placar da
 * conta vindo do banco em vez do MSW.
 */

test('o cadastro entra logado e a sessão sobrevive ao recarregamento', async ({ page }) => {
  await cadastrar(page)

  // O cadastro entra com o e-mail ainda não confirmado (§3.10).
  await expect(page.getByText(/e-mail ainda não foi confirmado/i)).toBeVisible()

  await page.reload()

  // Sem o cookie voltando ao servidor, o recarregamento cairia na tela pública.
  await expect(page.getByRole('link', { name: 'Conta' })).toBeVisible()
  await esperarODiaDaConta(page)
})

test('o cookie de sessão não é legível por script', async ({ page }) => {
  await cadastrar(page)

  // `HttpOnly` (§7.2/A05): XSS que consiga rodar não leva a sessão junto.
  const visivelAoScript = await page.evaluate(() => document.cookie)
  expect(visivelAoScript).not.toContain('tomatempo')
})

test('sair encerra a sessão e entrar de volta funciona', async ({ page }) => {
  const conta = await cadastrar(page)

  await page.goto('/conta')
  await page.getByRole('button', { name: 'Sair' }).click()

  await expect(page.getByTestId('signup-cta')).toBeVisible()
  await page.reload()
  await expect(page.getByRole('link', { name: 'Entrar' })).toBeVisible()

  await entrar(page, conta)
  await expect(page.getByRole('link', { name: 'Conta' })).toBeVisible()
})

test('o mesmo e-mail não vira uma segunda conta', async ({ page }) => {
  const conta = await cadastrar(page)

  await page.goto('/conta')
  await page.getByRole('button', { name: 'Sair' }).click()
  await expect(page.getByTestId('signup-cta')).toBeVisible()

  // Cadastro repetido responde igual ao de endereço novo e não abre sessão
  // (§3.10): a tela mostra o mesmo aviso neutro e a pessoa continua deslogada.
  await page.goto('/cadastrar')
  await page.locator('#cadastro-email').fill(conta.email)
  await page.locator('#cadastro-senha').fill('outra frase bem longa')
  await page.locator('#cadastro-apelido').fill(`${conta.apelido}b`)
  await page.getByRole('button', { name: 'Criar conta' }).click()

  await expect(page.getByText(/confira seu e-mail/i)).toBeVisible()
  await expect(page).toHaveURL(/\/cadastrar$/)
})

test('a conta nova começa zerada, mesmo com histórico anônimo no navegador', async ({ page }) => {
  // Relógio falso: 25 minutos de pomodoro anônimo em um pulo. O histórico local
  // fica com 1 pomodoro e 25 minutos — que é justamente o que não pode migrar.
  await page.clock.install()
  await page.goto('/')
  await iniciarFoco(page).click()
  await page.clock.fastForward('25:00')
  await expect(oDia(page)).toContainText(/1 pomodoro/)
  await expect(oDia(page)).toContainText(/25 min/)

  await cadastrar(page, novaConta())

  // Conta nova começa zerada (REGRAS §9): o placar agora é o do servidor.
  await expect(oDia(page)).toContainText(/0 pomodoros/)
  await expect(oDia(page)).toContainText(/0 min/)
})
