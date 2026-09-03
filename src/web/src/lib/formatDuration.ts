/**
 * Formata uma duração restante como MM:SS.
 *
 * Arredonda o segundo em curso para cima: faltando 24min59,4s ainda se lê
 * "25:00". Sem isso o contador pularia de 25:00 direto para 24:58 no primeiro
 * tique, porque o instante do clique quase nunca cai num segundo cheio.
 */
export function formatDuration(ms: number): string {
  const totalSeconds = Math.ceil(Math.max(0, ms) / 1000)
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60

  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
}
