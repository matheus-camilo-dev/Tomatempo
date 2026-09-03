import { describe, expect, it } from 'vitest'
import { manifest } from './manifest'

describe('manifesto do PWA', () => {
  it('deve identificar o produto', () => {
    expect(manifest.name).toContain('Tomatempo')
    expect(manifest.short_name).toBe('Tomatempo')
  })

  it('deve abrir em janela própria', () => {
    expect(manifest.display).toBe('standalone')
    expect(manifest.start_url).toBe('/')
  })

  it('deve declarar ícones de 192 e 512', () => {
    const sizes = manifest.icons.map((icon) => icon.sizes)

    expect(sizes).toContain('192x192')
    expect(sizes).toContain('512x512')
  })

  it('deve ter um ícone mascarável, exigido pelo Android', () => {
    expect(manifest.icons.some((icon) => icon.purpose === 'maskable')).toBe(true)
  })

  it('deve declarar cores de tema e de fundo', () => {
    expect(manifest.theme_color).toMatch(/^#[0-9a-f]{6}$/i)
    expect(manifest.background_color).toMatch(/^#[0-9a-f]{6}$/i)
  })
})
