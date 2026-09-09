/**
 * Dia local do usuário, como a API manda: `AAAA-MM-DD`, sem hora.
 *
 * Interpretar isso como instante e reimprimir no fuso da máquina desloca o dia
 * — "a partir de amanhã" vira "a partir de hoje" para quem está a oeste de
 * Greenwich. Por isso as partes são montadas em UTC e impressas em UTC.
 */
export function formatIsoDate(iso: string, locale: string): string {
  const ano = Number(iso.slice(0, 4))
  const mes = Number(iso.slice(5, 7))
  const dia = Number(iso.slice(8, 10))

  return new Intl.DateTimeFormat(locale, {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(new Date(Date.UTC(ano, mes - 1, dia)))
}
