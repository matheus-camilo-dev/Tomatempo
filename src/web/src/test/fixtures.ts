import type { Account, FreezeLedger } from '../lib/contracts'

/** Conta completa e comum. Cada teste sobrescreve só o campo que está em jogo. */
export function accountFixture(overrides: Partial<Account> = {}): Account {
  return {
    email: 'pessoa@exemplo.test',
    emailConfirmed: true,
    hasPassword: true,
    nickname: 'pessoa',
    onboardingRequired: false,
    timeZone: 'America/Sao_Paulo',
    locale: 'pt-BR',
    colorMode: 'system',
    themePresetId: null,
    avatarId: null,
    rankingOptOut: false,
    goal: { current: 4, upcoming: null, upcomingEffectiveFrom: null },
    streak: { current: 3, longest: 9 },
    freezes: { balance: 2 },
    ...overrides,
  }
}

export function freezeLedgerFixture(overrides: Partial<FreezeLedger> = {}): FreezeLedger {
  return {
    balance: 2,
    entries: [
      { delta: 2, reason: 'Signup', refDate: null, createdAt: '2026-09-01T12:00:00Z' },
    ],
    ...overrides,
  }
}
