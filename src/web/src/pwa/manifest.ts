/**
 * Manifesto do PWA, em módulo próprio para ser verificável por teste — a
 * alternativa seria enterrá-lo na configuração do Vite, onde um campo faltando
 * só apareceria numa auditoria manual.
 */

export interface ManifestIcon {
  readonly src: string
  readonly sizes: string
  readonly type: string
  readonly purpose?: 'any' | 'maskable'
}

export interface WebAppManifest {
  readonly name: string
  readonly short_name: string
  readonly description: string
  readonly start_url: string
  readonly scope: string
  readonly display: 'standalone' | 'fullscreen' | 'minimal-ui' | 'browser'
  readonly orientation: 'portrait' | 'landscape' | 'any'
  readonly theme_color: string
  readonly background_color: string
  readonly lang: string
  readonly icons: readonly ManifestIcon[]
}

export const manifest: WebAppManifest = {
  name: 'Tomatempo — timer pomodoro',
  short_name: 'Tomatempo',
  description:
    'Timer pomodoro de 25/5/15 com meta diária, sequência de dias e ranking semanal.',
  start_url: '/',
  scope: '/',
  display: 'standalone',
  orientation: 'portrait',
  // Slate-900: a cor do tema escuro, que é como o app abre na maioria dos casos.
  theme_color: '#0f172a',
  background_color: '#0f172a',
  lang: 'pt-BR',
  icons: [
    { src: '/icon-192.png', sizes: '192x192', type: 'image/png', purpose: 'any' },
    { src: '/icon-512.png', sizes: '512x512', type: 'image/png', purpose: 'any' },
    // O Android recorta o ícone em formatos variados; o mascarável reserva
    // margem para isso, evitando que o desenho saia cortado.
    { src: '/icon-maskable-512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
  ],
}
