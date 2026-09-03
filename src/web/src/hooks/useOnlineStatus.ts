import { useEffect, useState } from 'react'

/**
 * Estado da conexão.
 *
 * `navigator.onLine` responde se há interface de rede, não se a internet
 * funciona — é otimista por natureza. Serve para o aviso da fase 1; a partir da
 * fase 2, o que decide se um pomodoro pontua no ranking é o servidor ter
 * confirmado a sessão, nunca este valor (§3.4).
 */
export function useOnlineStatus(): boolean {
  const [online, setOnline] = useState(() => navigator.onLine)

  useEffect(() => {
    const goOnline = () => setOnline(true)
    const goOffline = () => setOnline(false)

    globalThis.addEventListener('online', goOnline)
    globalThis.addEventListener('offline', goOffline)

    return () => {
      globalThis.removeEventListener('online', goOnline)
      globalThis.removeEventListener('offline', goOffline)
    }
  }, [])

  return online
}
