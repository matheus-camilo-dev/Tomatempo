import { describe, expect, it } from 'vitest'
import { memoryStorage } from '../test/memoryStorage'
import {
  THEME_PREFERENCES,
  THEME_STORAGE_KEY,
  applyTheme,
  readPreference,
  resolveTheme,
  writePreference,
} from './theme'

describe('preferências disponíveis', () => {
  it('deve oferecer sistema, claro e escuro', () => {
    expect(THEME_PREFERENCES).toEqual(['system', 'light', 'dark'])
  })
})

describe('resolveTheme', () => {
  it('deve seguir o sistema quando a preferência é "system"', () => {
    expect(resolveTheme('system', true)).toBe('dark')
    expect(resolveTheme('system', false)).toBe('light')
  })

  it('deve sobrepor o sistema quando há escolha explícita', () => {
    expect(resolveTheme('light', true)).toBe('light')
    expect(resolveTheme('dark', false)).toBe('dark')
  })
})

describe('persistência', () => {
  it('deve devolver "system" quando nada foi escolhido', () => {
    expect(readPreference(memoryStorage())).toBe('system')
  })

  it('deve devolver a escolha guardada', () => {
    const storage = memoryStorage({ [THEME_STORAGE_KEY]: 'dark' })

    expect(readPreference(storage)).toBe('dark')
  })

  it('deve ignorar valor desconhecido no armazenamento', () => {
    const storage = memoryStorage({ [THEME_STORAGE_KEY]: 'neon' })

    expect(readPreference(storage)).toBe('system')
  })

  it('deve guardar a escolha explícita', () => {
    const storage = memoryStorage()

    writePreference(storage, 'light')

    expect(storage.getItem(THEME_STORAGE_KEY)).toBe('light')
  })

  it('deve apagar a escolha ao voltar para "system"', () => {
    // Guardar "system" registraria uma decisão que o usuário desfez; apagar
    // deixa o app seguir o sistema como se nunca tivesse havido escolha.
    const storage = memoryStorage({ [THEME_STORAGE_KEY]: 'dark' })

    writePreference(storage, 'system')

    expect(storage.getItem(THEME_STORAGE_KEY)).toBeNull()
  })

  it('deve funcionar sem armazenamento disponível', () => {
    expect(readPreference(null)).toBe('system')
    expect(() => writePreference(null, 'dark')).not.toThrow()
  })
})

describe('applyTheme', () => {
  it('deve marcar o elemento raiz no tema escuro', () => {
    const root = document.createElement('html')

    applyTheme(root, 'dark')

    expect(root.classList.contains('dark')).toBe(true)
    expect(root.style.colorScheme).toBe('dark')
  })

  it('deve desmarcar no tema claro', () => {
    const root = document.createElement('html')
    root.classList.add('dark')

    applyTheme(root, 'light')

    expect(root.classList.contains('dark')).toBe(false)
    expect(root.style.colorScheme).toBe('light')
  })
})
