using Tomatempo.Domain;
using Tomatempo.Domain.Sessions;

namespace Tomatempo.Domain.Tests.Sessions;

/// <summary>
/// Apuração de minutos de uma sessão de foco (§3.2). Critérios da §5: "Fechar a aba no
/// minuto 20 credita exatamente 20 minutos; fechar no minuto 3 credita 0" e "Manipular o
/// relógio do sistema não altera minuto algum".
///
/// Função pura sobre carimbos do servidor: o cliente não manda minuto, não manda duração e
/// não manda hora — só bate o heartbeat, e o servidor carimba (§7.3.3).
/// </summary>
public sealed class ApuracaoDeMinutosTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Heartbeats regulares, do jeito que um cliente vivo bate: a cada 30s.</summary>
    private static IReadOnlyList<DateTimeOffset> BatidasAte(TimeSpan duracao)
    {
        var batidas = new List<DateTimeOffset>();
        for (var t = RuleConstants.HeartbeatInterval; t <= duracao; t += RuleConstants.HeartbeatInterval)
        {
            batidas.Add(Inicio + t);
        }

        return batidas;
    }

    [Fact]
    public void Apuracao_DeveCreditarVinteMinutos_QuandoAAbaFechaNoMinutoVinte()
    {
        // Arrange
        var fim = Inicio + TimeSpan.FromMinutes(20);

        // Act
        var apurada = FocusSessionAccounting.Settle(Inicio, BatidasAte(TimeSpan.FromMinutes(20)), fim, fim);

        // Assert
        apurada.CountedMinutes.Should().Be(20);
        apurada.Completed.Should().BeFalse();
    }

    [Fact]
    public void Apuracao_DeveCreditarZero_QuandoAAbaFechaNoMinutoTres()
    {
        // Arrange — parcial abaixo de 5 minutos não conta para nada (§3.2).
        var fim = Inicio + TimeSpan.FromMinutes(3);

        // Act
        var apurada = FocusSessionAccounting.Settle(Inicio, BatidasAte(TimeSpan.FromMinutes(3)), fim, fim);

        // Assert
        apurada.CountedMinutes.Should().Be(0);
    }

    [Fact]
    public void Apuracao_DeveCreditarCinco_QuandoOParcialEncostaNoMinimo()
    {
        // Arrange — a fronteira do descarte, no lado que conta.
        var fim = Inicio + TimeSpan.FromMinutes(5);

        // Act
        var apurada = FocusSessionAccounting.Settle(Inicio, BatidasAte(TimeSpan.FromMinutes(5)), fim, fim);

        // Assert
        apurada.CountedMinutes.Should().Be(5);
    }

    [Fact]
    public void Apuracao_DeveTruncarNoUltimoHeartbeat_QuandoHouveBuracoNaCadeia()
    {
        // Arrange — bateu até o minuto 10 e sumiu; a aba fechada não rende tempo fantasma.
        var batidas = BatidasAte(TimeSpan.FromMinutes(10));
        var fim = Inicio + TimeSpan.FromMinutes(25);

        // Act
        var apurada = FocusSessionAccounting.Settle(Inicio, batidas, fim, fim);

        // Assert
        apurada.CountedMinutes.Should().Be(10);
        apurada.Completed.Should().BeFalse();
    }

    [Fact]
    public void Apuracao_DeveTruncarNoPrimeiroBuraco_QuandoACadeiaVoltaDepois()
    {
        // Arrange — bateu até o minuto 8, sumiu por 5 minutos e voltou a bater até o 20.
        // O que vale é o último heartbeat antes do buraco, não a soma dos pedaços.
        var batidas = BatidasAte(TimeSpan.FromMinutes(8)).ToList();
        for (var t = TimeSpan.FromMinutes(13); t <= TimeSpan.FromMinutes(20); t += RuleConstants.HeartbeatInterval)
        {
            batidas.Add(Inicio + t);
        }

        var fim = Inicio + TimeSpan.FromMinutes(20);

        // Act
        var apurada = FocusSessionAccounting.Settle(Inicio, batidas, fim, fim);

        // Assert
        apurada.CountedMinutes.Should().Be(8);
    }

    [Fact]
    public void Apuracao_NaoDevePassarDeVinteECinco_QuandoASessaoFicouAbertaAlemDoCiclo()
    {
        // Arrange — o ciclo é fixo em 25min (§3.1); ficar com a aba aberta não rende mais.
        var fim = Inicio + TimeSpan.FromMinutes(40);

        // Act
        var apurada = FocusSessionAccounting.Settle(Inicio, BatidasAte(TimeSpan.FromMinutes(40)), fim, fim);

        // Assert
        apurada.CountedMinutes.Should().Be(25);
    }

    [Fact]
    public void Apuracao_DeveConcluir_QuandoChegouAVinteECincoComHeartbeatRecente()
    {
        // Arrange
        var fim = Inicio + TimeSpan.FromMinutes(25);

        // Act
        var apurada = FocusSessionAccounting.Settle(Inicio, BatidasAte(TimeSpan.FromMinutes(25)), fim, fim);

        // Assert
        apurada.CountedMinutes.Should().Be(25);
        apurada.Completed.Should().BeTrue();
    }

    [Fact]
    public void Apuracao_NaoDeveConcluir_QuandoOUltimoHeartbeatEstaLongeDoFim()
    {
        // Arrange — bateu até o minuto 26 e o fim só chegou no 28: passou dos 25 minutos, mas
        // ninguém estava lá no fim. Vira parcial, não pomodoro concluído (§3.2).
        var batidas = BatidasAte(TimeSpan.FromMinutes(26));
        var fim = Inicio + TimeSpan.FromMinutes(28);

        // Act
        var apurada = FocusSessionAccounting.Settle(Inicio, batidas, fim, fim);

        // Assert
        apurada.CountedMinutes.Should().Be(25);
        apurada.Completed.Should().BeFalse();
    }

    [Fact]
    public void Apuracao_NaoDeveContarAlemDoAgoraDoServidor_QuandoOFimVemAdiantado()
    {
        // Arrange — relógio da máquina adiantado uma hora. O `endedAt` do cliente não manda:
        // o corte é o `serverNow` (§5, "manipular o relógio do sistema não altera minuto algum").
        var batidas = BatidasAte(TimeSpan.FromMinutes(10));
        var agoraDoServidor = Inicio + TimeSpan.FromMinutes(10);
        var fimMentiroso = Inicio + TimeSpan.FromMinutes(70);

        // Act
        var apurada = FocusSessionAccounting.Settle(Inicio, batidas, fimMentiroso, agoraDoServidor);

        // Assert
        apurada.CountedMinutes.Should().Be(10);
        apurada.Completed.Should().BeFalse();
    }

    [Fact]
    public void Apuracao_DeveCreditarZero_QuandoNaoHouveHeartbeatAlgum()
    {
        // Arrange — sessão aberta e abandonada antes do primeiro heartbeat.
        var fim = Inicio + TimeSpan.FromMinutes(20);

        // Act
        var apurada = FocusSessionAccounting.Settle(Inicio, [], fim, fim);

        // Assert
        apurada.CountedMinutes.Should().Be(0);
    }

    [Fact]
    public void Apuracao_DeveArredondarParaBaixo_QuandoOParcialTemSegundosQuebrados()
    {
        // Arrange — 12min37s viram 12 minutos, nunca 13 (§3.2: `floor`).
        var duracao = TimeSpan.FromMinutes(12) + TimeSpan.FromSeconds(37);
        var fim = Inicio + duracao;

        // Act
        var apurada = FocusSessionAccounting.Settle(Inicio, BatidasAte(duracao), fim, fim);

        // Assert
        apurada.CountedMinutes.Should().Be(12);
    }

    [Fact]
    public void Apuracao_DeveIgnorarAOrdemDeChegadaDosHeartbeats()
    {
        // Arrange — heartbeats podem chegar fora de ordem por rede; a cadeia é cronológica.
        var batidas = BatidasAte(TimeSpan.FromMinutes(10)).Reverse().ToList();
        var fim = Inicio + TimeSpan.FromMinutes(10);

        // Act
        var apurada = FocusSessionAccounting.Settle(Inicio, batidas, fim, fim);

        // Assert
        apurada.CountedMinutes.Should().Be(10);
    }
}
