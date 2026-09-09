using Tomatempo.Domain.Time;

namespace Tomatempo.Domain.Tests.Time;

/// <summary>
/// Dia local do usuário (§3.6). Critério da §5: "Suíte de domínio cobre virada de dia,
/// horário de verão, offsets :30 e :45 e virada de ano".
///
/// Tudo por biblioteca de fuso, nunca por aritmética de offset fixo: offset fixo erra o dia
/// em toda transição de horário de verão, e erra sempre em Kathmandu e Chatham.
/// </summary>
public sealed class DiaLocalTests
{
    private static TimeZoneInfo Fuso(string id) => TimeZoneInfo.FindSystemTimeZoneById(id);

    [Fact]
    public void DiaLocal_DeveUsarODiaDoUsuario_QuandoOUtcJaVirou()
    {
        // Arrange — 02:00 UTC de 10/09 ainda é 23:00 de 09/09 em São Paulo.
        var instante = new DateTimeOffset(2026, 9, 10, 2, 0, 0, TimeSpan.Zero);

        // Act
        var dia = LocalDay.Of(instante, Fuso("America/Sao_Paulo"));

        // Assert
        dia.Should().Be(new DateOnly(2026, 9, 9));
    }

    [Fact]
    public void DiaLocal_DeveVirarAntesDoUtc_QuandoOFusoEstaAdiantado()
    {
        // Arrange — 20:00 UTC de 09/09 já é 01:30 de 10/09 em Kathmandu (+5:45).
        var instante = new DateTimeOffset(2026, 9, 9, 20, 0, 0, TimeSpan.Zero);

        // Act
        var dia = LocalDay.Of(instante, Fuso("Asia/Kathmandu"));

        // Assert
        dia.Should().Be(new DateOnly(2026, 9, 10));
    }

    [Fact]
    public void DiaLocal_DeveRespeitarOffsetDeMeiaHora()
    {
        // Arrange — 18:45 UTC de 09/09 já é 00:15 de 10/09 na Índia (+5:30).
        var instante = new DateTimeOffset(2026, 9, 9, 18, 45, 0, TimeSpan.Zero);

        // Act
        var dia = LocalDay.Of(instante, Fuso("Asia/Kolkata"));

        // Assert
        dia.Should().Be(new DateOnly(2026, 9, 10));
    }

    [Fact]
    public void DiaLocal_DeveRespeitarOffsetDeQuarentaECincoMinutos()
    {
        // Arrange — 15:20 UTC de 09/09 já é 00:05 de 10/09 em Eucla (+8:45).
        var instante = new DateTimeOffset(2026, 9, 9, 15, 20, 0, TimeSpan.Zero);

        // Act
        var dia = LocalDay.Of(instante, Fuso("Australia/Eucla"));

        // Assert
        dia.Should().Be(new DateOnly(2026, 9, 10));
    }

    [Fact]
    public void DiaLocal_DeveAcompanharOHorarioDeVerao()
    {
        // Arrange — Nova York entra no horário de verão em 08/03/2026: antes é -5, depois -4.
        // 04:30 UTC vira 23:30 do dia anterior no inverno e 00:30 do mesmo dia no verão.
        var noInverno = new DateTimeOffset(2026, 3, 1, 4, 30, 0, TimeSpan.Zero);
        var noVerao = new DateTimeOffset(2026, 6, 1, 4, 30, 0, TimeSpan.Zero);
        var novaYork = Fuso("America/New_York");

        // Act
        var diaNoInverno = LocalDay.Of(noInverno, novaYork);
        var diaNoVerao = LocalDay.Of(noVerao, novaYork);

        // Assert
        diaNoInverno.Should().Be(new DateOnly(2026, 2, 28));
        diaNoVerao.Should().Be(new DateOnly(2026, 6, 1));
    }

    [Fact]
    public void DiaLocal_DeveViraroAno_QuandoAMeiaNoiteLocalPassa()
    {
        // Arrange — 03:00 UTC de 01/01/2027 ainda é 31/12/2026 em São Paulo.
        var instante = new DateTimeOffset(2027, 1, 1, 1, 0, 0, TimeSpan.Zero);

        // Act
        var dia = LocalDay.Of(instante, Fuso("America/Sao_Paulo"));

        // Assert
        dia.Should().Be(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public void DiasAFechar_DeveTrazerNada_QuandoODiaCorrenteAindaNaoTerminou()
    {
        // Arrange — meio-dia local: o dia de hoje ainda está em curso e não fecha.
        var agora = new DateTimeOffset(2026, 9, 9, 15, 0, 0, TimeSpan.Zero);

        // Act
        var pendentes = LocalDay.PendingClosures(
            lastClosed: new DateOnly(2026, 9, 8), agora, Fuso("America/Sao_Paulo"));

        // Assert
        pendentes.Should().BeEmpty();
    }

    [Fact]
    public void DiasAFechar_DeveTrazerODiaQueAcabou_QuandoAMeiaNoiteLocalPassou()
    {
        // Arrange — 03:05 UTC de 10/09 é 00:05 local: o dia 09 acabou de terminar.
        var agora = new DateTimeOffset(2026, 9, 10, 3, 5, 0, TimeSpan.Zero);

        // Act
        var pendentes = LocalDay.PendingClosures(
            lastClosed: new DateOnly(2026, 9, 8), agora, Fuso("America/Sao_Paulo"));

        // Assert
        pendentes.Should().Equal(new DateOnly(2026, 9, 9));
    }

    [Fact]
    public void DiasAFechar_DeveTrazerTodosOsDiasEmAberto_QuandoOJobFicouForaDoAr()
    {
        // Arrange — três dias sem fechar nada; nenhum pode ser pulado.
        var agora = new DateTimeOffset(2026, 9, 10, 3, 5, 0, TimeSpan.Zero);

        // Act
        var pendentes = LocalDay.PendingClosures(
            lastClosed: new DateOnly(2026, 9, 6), agora, Fuso("America/Sao_Paulo"));

        // Assert
        pendentes.Should().Equal(
            new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 8), new DateOnly(2026, 9, 9));
    }

    [Fact]
    public void DiasAFechar_DeveTrazerNada_QuandoODiaJaFoiFechado()
    {
        // Arrange — reprocessar não pode refazer o que já foi selado (§3.6, idempotência).
        var agora = new DateTimeOffset(2026, 9, 10, 3, 5, 0, TimeSpan.Zero);

        // Act
        var pendentes = LocalDay.PendingClosures(
            lastClosed: new DateOnly(2026, 9, 9), agora, Fuso("America/Sao_Paulo"));

        // Assert
        pendentes.Should().BeEmpty();
    }

    [Fact]
    public void DiasAFechar_DeveComecarPeloDiaDaConta_QuandoNuncaHouveFechamento()
    {
        // Arrange — conta criada em 08/09, nada fechado ainda.
        var agora = new DateTimeOffset(2026, 9, 10, 3, 5, 0, TimeSpan.Zero);

        // Act
        var pendentes = LocalDay.PendingClosures(
            lastClosed: new DateOnly(2026, 9, 7), agora, Fuso("America/Sao_Paulo"));

        // Assert
        pendentes.Should().Equal(new DateOnly(2026, 9, 8), new DateOnly(2026, 9, 9));
    }
}
