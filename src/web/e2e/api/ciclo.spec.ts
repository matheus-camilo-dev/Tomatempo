import { expect, test } from '@playwright/test'
import {
  abandonar,
  cadastrar,
  iniciarFoco,
  iniciarFocoNoServidor,
  oDia,
} from './conta'

/**
 * O ciclo de foco de quem está logado, ponta a ponta (§3.2, §3.3, §3.4).
 *
 * Aqui o relógio falso do navegador não é conveniência de teste: ele É o
 * cenário. O servidor conta pelo relógio dele, e é isso que estes casos provam
 * — adiantar a máquina não rende minuto, e o ciclo cumprido offline rende,
 * porque o cliente declara o que fez e o servidor valida.
 */

const CHAVE_DO_TIMER = 'tomatempo:timer:v1'

test('recarregar no meio do foco recupera o ciclo pelo servidor', async ({ page }) => {
  await cadastrar(page)

  await iniciarFocoNoServidor(page)
  await expect(page.getByRole('timer')).not.toHaveText('25:00')

  // O estado local do timer é apagado de propósito: sobrando, o ciclo voltaria
  // do `localStorage` e o caso não provaria nada sobre `GET /sessions/active`.
  await page.evaluate((chave) => localStorage.removeItem(chave), CHAVE_DO_TIMER)
  await page.reload()

  await expect(abandonar(page)).toBeVisible()
  await expect(page.getByRole('timer')).not.toHaveText('25:00')
  await expect(page.getByRole('timer')).toHaveText(/^2[34]:/)
})

test('adiantar o relógio da máquina não vira minuto no dia da conta', async ({ page }) => {
  await page.clock.install()
  await cadastrar(page)

  await iniciarFocoNoServidor(page)

  // A máquina acha que se passaram seis minutos; o servidor viu segundos.
  await page.clock.fastForward('06:00')
  await abandonar(page).click()

  // Zero, e não os seis minutos que o relógio local daria: quem apura é o
  // servidor (REGRAS §9).
  await expect(oDia(page)).toContainText(/0 min/)
  await expect(oDia(page)).toContainText(/0 pomodoros/)
})

test('o ciclo cumprido offline é creditado ao reconectar', async ({ page, context }) => {
  await page.clock.install()
  await cadastrar(page)
  await expect(oDia(page)).toContainText(/0 pomodoros/)

  await context.setOffline(true)

  // Sem rede a sessão não abre no servidor: o ciclo corre local e vai para a
  // fila (§3.4).
  await iniciarFoco(page).click()
  await expect(abandonar(page)).toBeVisible()
  await page.clock.fastForward('25:00')
  await expect(iniciarFoco(page)).toBeHidden()

  await context.setOffline(false)
  await page.evaluate(() => globalThis.dispatchEvent(new Event('online')))

  // O lote subiu, o servidor aceitou e o dia da conta passou a contá-lo.
  await expect(oDia(page)).toContainText(/1 pomodoro/, { timeout: 20_000 })
  await expect(oDia(page)).toContainText(/25 min/)
})

test('a segunda aba mostra o mesmo ciclo, em vez de tomá-lo', async ({ page, context }) => {
  await cadastrar(page)

  await iniciarFocoNoServidor(page)

  const segunda = await context.newPage()
  // Sem o ciclo local da primeira: as duas abas dividem o mesmo `localStorage`,
  // e o que se quer ver é a sessão voltando do servidor, não do navegador.
  await segunda.addInitScript((chave) => localStorage.removeItem(chave), CHAVE_DO_TIMER)
  await segunda.goto('/')

  // A segunda recupera a sessão por `GET /sessions/active` (§3.2) — abrir a
  // tela não é `start`, então não toma a sessão de ninguém.
  await expect(abandonar(segunda)).toBeVisible()
  await expect(segunda.getByRole('timer')).toHaveText(/^2[34]:/)
  await expect(page.getByTestId('session-lost')).toBeHidden()

  await segunda.close()
})

test('começar um ciclo na segunda aba faz a primeira parar de pontuar', async ({ page, context }) => {
  await page.clock.install()
  await cadastrar(page)

  await iniciarFocoNoServidor(page)

  const segunda = await context.newPage()
  await segunda.addInitScript((chave) => localStorage.removeItem(chave), CHAVE_DO_TIMER)
  await segunda.goto('/')
  await expect(abandonar(segunda)).toBeVisible()

  // A segunda encerra o ciclo recuperado e começa o dela: é o `start` que
  // assume a sessão, e é ele que tira a sessão da primeira aba (§3.3).
  await abandonar(segunda).click()
  await expect(iniciarFoco(segunda)).toBeVisible()

  // Esperar a resposta, e não o botão: o timer começa pelo relógio local mesmo
  // sem servidor (§3.4), então "Abandonar" apareceria antes de a sessão existir
  // — e o batimento da primeira aba ainda encontraria a sessão dela de pé.
  await iniciarFocoNoServidor(segunda)

  // A primeira só descobre no batimento seguinte, que é onde o 409 chega.
  await page.clock.fastForward('00:31')

  await expect(page.getByTestId('session-lost')).toBeVisible()
  await expect(iniciarFoco(page)).toBeVisible()

  await segunda.close()
})
