import { readFileSync } from 'node:fs'
import { defineConfig, devices } from '@playwright/test'

/**
 * E2E da fase 2 — o que só existe com a API e o Postgres de pé (§6.3).
 *
 * A suíte da fase 1 (`playwright.config.ts`) continua rodando só com o front:
 * separar as duas é o que mantém aquela rápida e sem dependência de banco.
 *
 * Antes de rodar localmente:
 *
 *   docker compose up -d postgres mailpit
 *   npm run test:e2e:api
 *
 * O banco é o `tomatempo_e2e`, criado na subida pela migração — separado do
 * `tomatempo` de desenvolvimento, para uma suíte que cria contas não sujar o
 * banco em que você trabalha.
 */

const API_PORT = 8081
const WEB_PORT = 4174

/**
 * A senha do Postgres nunca é escrita aqui: ela vem do ambiente, e no
 * desenvolvimento local mora no `.env`, que é ignorado pelo git (§7.5). Em
 * repositório público, senha versionada é senha vazada com desculpa.
 */
function senhaDoPostgres(): string {
  const doAmbiente = process.env.POSTGRES_PASSWORD
  if (doAmbiente) return doAmbiente

  try {
    const env = readFileSync(new URL('../../.env', import.meta.url), 'utf8')
    const linha = env.split('\n').find((l) => l.startsWith('POSTGRES_PASSWORD='))
    const valor = linha?.slice('POSTGRES_PASSWORD='.length).trim()
    if (valor) return valor
  } catch {
    // Sem `.env` a mensagem abaixo diz o que fazer; ler o arquivo é conveniência.
  }

  throw new Error(
    'POSTGRES_PASSWORD não está definida. Copie .env.example para .env (veja §7.5) ' +
      'ou exporte a variável antes de rodar o E2E da fase 2.',
  )
}

/**
 * Montada por pares, e não por texto com `Password=` interpolado: escrita
 * daquele jeito, a linha casa com a regra `dotnet-connection-string` do
 * gitleaks (§7.5) e o commit é bloqueado. A alternativa seria abrir uma exceção
 * no scanner para este arquivo — o que também deixaria passar uma senha de
 * verdade escrita aqui um dia. Assim o detector continua armado.
 */
const conexao = Object.entries({
  Host: 'localhost',
  Port: '5432',
  Database: 'tomatempo_e2e',
  Username: 'tomatempo',
  Password: senhaDoPostgres(),
})
  .map(([chave, valor]) => `${chave}=${valor}`)
  .join(';')

export default defineConfig({
  testDir: './e2e/api',
  // Um worker: os casos compartilham o mesmo Postgres, e sessão de foco é única
  // por usuário (§3.3) — paralelismo aqui compraria corrida sem comprar tempo.
  workers: 1,
  fullyParallel: false,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  // Timer de 25 minutos adiantado pelo relógio falso do navegador ainda custa
  // milhares de tiques reais; o padrão de 30s não cobre.
  timeout: 120_000,
  use: {
    baseURL: `http://localhost:${WEB_PORT}`,
    // Idioma fixo: sem isso a tela sai no idioma do runner e as asserções
    // precisariam casar dois idiomas a cada linha.
    locale: 'pt-BR',
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      command: `dotnet run --project ../api/Tomatempo.Api --urls http://localhost:${API_PORT}`,
      url: `http://localhost:${API_PORT}/health`,
      env: {
        // `Development` é o que faz a migração rodar na subida (§10) e mantém os
        // dois jobs de fundo ligados, que é como a API roda de verdade.
        ASPNETCORE_ENVIRONMENT: 'Development',
        ConnectionStrings__Db: conexao,
        // O cadastro manda e-mail de confirmação, e sem SMTP escutando ele
        // estoura: o mailpit do compose é o destino (§10).
        Smtp__Host: process.env.SMTP_HOST ?? 'localhost',
        Smtp__Port: process.env.SMTP_PORT ?? '1025',
        App__PublicUrl: `http://localhost:${WEB_PORT}`,
      },
      timeout: 180_000,
      reuseExistingServer: !process.env.CI,
      stdout: 'ignore',
      stderr: 'pipe',
    },
    {
      command: `npm run build && npm run preview -- --port ${WEB_PORT} --strictPort`,
      url: `http://localhost:${WEB_PORT}`,
      // O proxy do preview lê daqui: é o que põe a API na mesma origem do SPA e
      // deixa o cookie de sessão viajar.
      env: { VITE_API_URL: `http://localhost:${API_PORT}` },
      timeout: 180_000,
      reuseExistingServer: !process.env.CI,
    },
  ],
})
