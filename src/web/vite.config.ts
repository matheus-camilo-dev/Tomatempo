import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'
import { defineConfig } from 'vitest/config'
import { manifest } from './src/pwa/manifest.ts'

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    VitePWA({
      // O app é pequeno e o usuário costuma deixá-lo aberto por horas:
      // atualizar sozinho evita ficar preso a uma versão antiga por dias.
      registerType: 'autoUpdate',
      injectRegister: 'auto',
      // Fonte única do manifesto, verificada por teste em src/pwa/manifest.ts.
      // O tipo do plugin exige arranjos mutáveis; o nosso é readonly de
      // propósito, para ninguém alterar o manifesto em tempo de execução.
      manifest: { ...manifest, icons: [...manifest.icons] },
      includeAssets: ['favicon.svg', 'apple-touch-icon.png'],
      workbox: {
        globPatterns: ['**/*.{js,css,html,png,svg,woff2}'],
        // Toda navegação cai no index.html guardado, que é o que faz o app
        // abrir offline mesmo em rota que o usuário digitou.
        navigateFallback: '/index.html',
      },
    }),
  ],
  test: {
    environment: 'jsdom',
    // e2e é do Playwright, que tem runner próprio.
    exclude: ['node_modules/**', 'e2e/**', 'dist/**'],
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    coverage: {
      provider: 'v8',
      reporter: ['text', 'lcov'],
      include: ['src/**/*.{ts,tsx}'],
      exclude: ['src/**/*.test.{ts,tsx}', 'src/test/**', 'src/main.tsx', 'src/vite-env.d.ts'],
      // Gate da §6.6: 90% na lógica pura (domínio e hooks). O resto é medido,
      // reportado e não barra o merge.
      thresholds: {
        'src/domain/**': { lines: 90, branches: 90, functions: 90, statements: 90 },
        'src/hooks/**': { lines: 90, branches: 90, functions: 90, statements: 90 },
      },
    },
  },
})
