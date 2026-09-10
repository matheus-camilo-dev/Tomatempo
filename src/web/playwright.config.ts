import { defineConfig, devices } from '@playwright/test'

/**
 * E2E roda contra o **build de produção**, não o servidor de desenvolvimento:
 * service worker, manifesto e precache só existem depois do build, e são
 * justamente os critérios que os testes unitários não alcançam (§5, fase 1).
 */
export default defineConfig({
  testDir: './e2e',
  // `e2e/api` tem config própria (`playwright.api.config.ts`): aqueles casos
  // precisam da API e do Postgres de pé, e esta suíte roda só com o front.
  testIgnore: 'api/**',
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  use: {
    baseURL: 'http://localhost:4173',
    trace: 'on-first-retry',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
  webServer: {
    command: 'npm run build && npm run preview -- --port 4173 --strictPort',
    url: 'http://localhost:4173',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
})
