namespace Tomatempo.Domain.Time;

/// <summary>
/// O dia do usuário (§3.6). Sempre pela biblioteca de fuso, nunca por aritmética de offset
/// fixo: offset fixo erra o dia em toda transição de horário de verão, e erra o ano inteiro
/// em Kathmandu, Kolkata, Eucla e Chatham.
/// </summary>
public static class LocalDay
{
    /// <summary>Que dia é, para quem vive neste fuso, no instante dado.</summary>
    public static DateOnly Of(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    /// <summary>
    /// Os dias que já terminaram para este usuário e ainda não foram selados, do mais antigo
    /// para o mais novo.
    ///
    /// O job roda de hora em hora (§3.6) e pode ter ficado fora do ar: devolver a lista
    /// inteira, e não só o último, é o que impede um dia de ser pulado. Reprocessar um dia já
    /// fechado devolve lista vazia — a idempotência mora aqui.
    /// </summary>
    public static IReadOnlyList<DateOnly> PendingClosures(
        DateOnly lastClosed,
        DateTimeOffset now,
        TimeZoneInfo zone)
    {
        // O dia corrente ainda está em curso; o último que fechou é o anterior a ele.
        var ultimoTerminado = Of(now, zone).AddDays(-1);

        var pendentes = new List<DateOnly>();
        for (var dia = lastClosed.AddDays(1); dia <= ultimoTerminado; dia = dia.AddDays(1))
        {
            pendentes.Add(dia);
        }

        return pendentes;
    }
}
