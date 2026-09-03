import { expect, test } from '@playwright/test'

/**
 * Cobre o que a suíte unitária não alcança: navegador de verdade, service
 * worker, manifesto e a rede caindo. Os critérios de comportamento do timer
 * ficam nos testes unitários, que são muito mais rápidos.
 */

test('a tela abre pronta para o primeiro pomodoro', async ({ page }) => {
  await page.goto('/')

  await expect(page).toHaveTitle('Tomatempo')
  await expect(page.getByRole('timer')).toHaveText('25:00')
  await expect(page.getByRole('button', { name: /iniciar foco|start focus/i })).toBeVisible()
})

test('o contador anda de verdade no navegador', async ({ page }) => {
  await page.goto('/')

  await page.getByRole('button', { name: /iniciar foco|start focus/i }).click()
  await expect(page.getByRole('timer')).not.toHaveText('25:00', { timeout: 3_000 })
  await expect(page.getByRole('button', { name: /abandonar|stop/i })).toBeVisible()
})

test('o tema escuro muda o fundo da página', async ({ page }) => {
  await page.goto('/')
  const background = () => page.evaluate(() => getComputedStyle(document.body).backgroundColor)

  await page.getByRole('button', { name: /^claro$|^light$/i }).click()
  const light = await background()
  await page.getByRole('button', { name: /^escuro$|^dark$/i }).click()

  expect(await background()).not.toBe(light)
})

test('o idioma troca a tela inteira', async ({ page }) => {
  await page.goto('/')

  await page.getByRole('button', { name: 'English' }).click()

  await expect(page.getByRole('button', { name: 'Start focus' })).toBeVisible()
})

test('o app se instala como PWA', async ({ page }) => {
  await page.goto('/')

  await expect(page.locator('link[rel=manifest]')).toHaveAttribute(
    'href',
    '/manifest.webmanifest',
  )

  const manifest = await page.request.get('/manifest.webmanifest').then((r) => r.json())
  expect(manifest.display).toBe('standalone')
  expect(manifest.icons.map((icon: { sizes: string }) => icon.sizes)).toContain('512x512')

  const registered = await page.evaluate(async () => {
    const registration = await navigator.serviceWorker.getRegistration()
    return Boolean(registration?.active ?? registration?.installing ?? registration?.waiting)
  })
  expect(registered).toBe(true)
})

test('o app abre sem rede, servido pelo service worker', async ({ page, context }) => {
  await page.goto('/')
  await page.evaluate(() => navigator.serviceWorker.ready)

  await context.setOffline(true)
  await page.reload({ waitUntil: 'domcontentloaded' })

  await expect(page.getByRole('timer')).toHaveText('25:00')
  await context.setOffline(false)
})

test('o aviso de offline aparece quando a conexão cai', async ({ page, context }) => {
  await page.goto('/')

  await context.setOffline(true)

  // Sem recarregar: o Playwright reseta `navigator.onLine` para true em
  // documento novo, então a verificação após reload testaria a emulação, não
  // o app. Em navegador real a página carregada do cache reporta offline.
  await expect(page.getByTestId('offline-banner')).toBeVisible()
  await context.setOffline(false)
  await expect(page.getByTestId('offline-banner')).toBeHidden()
})
