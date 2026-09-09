/**
 * `code` do problem+json → chave do catálogo. A tela traduz pelo code e nunca
 * mostra o texto que veio do servidor (§4).
 *
 * A lista é fechada de propósito: code que o front não conhece cai na mensagem
 * genérica, senão o i18next renderizaria a própria chave crua na tela.
 */
export const KNOWN_PROBLEM_CODES = [
  'unauthenticated',
  'forbidden',
  'invalid_request',
  'invalid_credentials',
  'invalid_email',
  'invalid_nickname',
  'nickname_taken',
  'nickname_locked',
  'invalid_goal',
  'invalid_time_zone',
  'invalid_locale',
  'invalid_color_mode',
  'weak_password',
  'invalid_token',
  'onboarding_required',
  'onboarding_already_completed',
  'too_many_requests',
  'unsupported_media_type',
  'internal_error',
  // Não são problem+json: chegam no `?erro=` do redirecionamento do Google.
  'external_login_failed',
  'external_email_not_verified',
  // Nascem no cliente, quando a chamada nem sai.
  'network',
] as const

export type ProblemCode = (typeof KNOWN_PROBLEM_CODES)[number]

export function problemMessageKey(code: string | null | undefined): string {
  return isKnown(code) ? `error.${code}` : 'error.unknown'
}

function isKnown(code: string | null | undefined): code is ProblemCode {
  return KNOWN_PROBLEM_CODES.includes(code as ProblemCode)
}
