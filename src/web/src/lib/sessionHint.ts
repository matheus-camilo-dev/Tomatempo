/**
 * Pista de que existe sessão nesta máquina — um booleano, nunca um
 * identificador (§7.2/A01). Serve para decidir se o app pergunta quem é o
 * usuário: sem ela, visitante anônimo usa o timer sem tocar na API.
 *
 * Não é fonte de verdade. Quem decide é o cookie: a pista pode estar de pé com
 * o cookie já vencido, e o primeiro 401 a apaga.
 */
export const SESSION_HINT_KEY = 'tomatempo:session'

const ouvintes = new Set<() => void>()

export function hasSessionHint(storage: Storage | null = globalThis.localStorage): boolean {
  return storage?.getItem(SESSION_HINT_KEY) === '1'
}

export function rememberSession(storage: Storage | null = globalThis.localStorage): void {
  gravar(storage, '1')
}

export function forgetSession(storage: Storage | null = globalThis.localStorage): void {
  gravar(storage, null)
}

/** Assinatura para `useSyncExternalStore`: entrar e sair têm que repintar a tela. */
export function subscribeSessionHint(ouvinte: () => void): () => void {
  ouvintes.add(ouvinte)

  return () => void ouvintes.delete(ouvinte)
}

function gravar(storage: Storage | null, valor: string | null): void {
  if (storage) {
    try {
      if (valor === null) storage.removeItem(SESSION_HINT_KEY)
      else storage.setItem(SESSION_HINT_KEY, valor)
    } catch {
      // Cota estourada ou armazenamento bloqueado: a sessão segue valendo nesta
      // aba, só não sobrevive ao recarregamento.
    }
  }

  for (const ouvinte of ouvintes) ouvinte()
}
