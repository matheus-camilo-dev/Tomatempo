using Tomatempo.Domain;
using Tomatempo.Domain.Sessions;

namespace Tomatempo.Domain.Tests.Sessions;

/// <summary>
/// Apuração a partir do estado persistido (§3.2). A tabela `focus_sessions` guarda
/// `last_heartbeat_at_utc`, e não a cadeia inteira (§2) — guardar cada batida seria uma linha
/// a cada 30 segundos por sessão, para responder uma pergunta que só precisa do último ponto
/// válido.
///
/// A coluna só avança enquanto a cadeia está inteira: batida que chega atrasada é aceita mas
/// não move o ponto, e é isso que faz a cadeia quebrada continuar quebrada.
/// </summary>
public sealed class ApuracaoPeloUltimoHeartbeatTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Apuracao_DeveContarAteOFim_QuandoOUltimoHeartbeatEstaPerto()
    {
        // Arrange — bateu no minuto 20 e fechou logo depois.
        var ultimaBatida = Inicio + TimeSpan.FromMinutes(20);
        var fim = ultimaBatida + TimeSpan.FromSeconds(10);

        // Act
        var apurada = FocusSessionAccounting.SettleFromLastHeartbeat(Inicio, ultimaBatida, fim, fim);

        // Assert
        apurada.CountedMinutes.Should().Be(20);
    }

    [Fact]
    public void Apuracao_DeveTruncarNoUltimoHeartbeat_QuandoOFimChegouMuitoDepois()
    {
        // Arrange — bateu até o minuto 10, sumiu, e o encerramento só veio no 25.
        var ultimaBatida = Inicio + TimeSpan.FromMinutes(10);
        var fim = Inicio + TimeSpan.FromMinutes(25);

        // Act
        var apurada = FocusSessionAccounting.SettleFromLastHeartbeat(Inicio, ultimaBatida, fim, fim);

        // Assert
        apurada.CountedMinutes.Should().Be(10);
        apurada.Completed.Should().BeFalse();
    }

    [Fact]
    public void Apuracao_DeveConcluir_QuandoFechouOCicloComBatidaRecente()
    {
        // Arrange
        var ultimaBatida = Inicio + RuleConstants.FocusDuration;

        // Act
        var apurada = FocusSessionAccounting.SettleFromLastHeartbeat(
            Inicio, ultimaBatida, ultimaBatida, ultimaBatida);

        // Assert
        apurada.CountedMinutes.Should().Be(25);
        apurada.Completed.Should().BeTrue();
    }

    [Fact]
    public void Apuracao_DeveDescartar_QuandoOPontoValidoNemChegouAosCincoMinutos()
    {
        // Arrange
        var ultimaBatida = Inicio + TimeSpan.FromMinutes(3);
        var fim = Inicio + TimeSpan.FromMinutes(30);

        // Act
        var apurada = FocusSessionAccounting.SettleFromLastHeartbeat(Inicio, ultimaBatida, fim, fim);

        // Assert
        apurada.CountedMinutes.Should().Be(0);
    }

    [Fact]
    public void Apuracao_NaoDeveContarAlemDoAgoraDoServidor()
    {
        // Arrange — o `endedAt` que o cliente provoca não passa do relógio do servidor (§5).
        var ultimaBatida = Inicio + TimeSpan.FromMinutes(10);
        var agoraDoServidor = Inicio + TimeSpan.FromMinutes(10);
        var fimMentiroso = Inicio + TimeSpan.FromHours(2);

        // Act
        var apurada = FocusSessionAccounting.SettleFromLastHeartbeat(
            Inicio, ultimaBatida, fimMentiroso, agoraDoServidor);

        // Assert
        apurada.CountedMinutes.Should().Be(10);
    }

    [Fact]
    public void Apuracao_DeveCreditarZero_QuandoNaoHouveHeartbeatAlgum()
    {
        // Arrange — sessão aberta e largada antes da primeira batida.
        var fim = Inicio + TimeSpan.FromMinutes(20);

        // Act
        var apurada = FocusSessionAccounting.SettleFromLastHeartbeat(Inicio, lastHeartbeat: null, fim, fim);

        // Assert
        apurada.CountedMinutes.Should().Be(0);
    }

    [Fact]
    public void CadeiaInteira_DeveAceitarBatidaDentroDoLimite()
    {
        // Arrange — é o que decide se a coluna avança a cada heartbeat.
        var anterior = Inicio + TimeSpan.FromMinutes(5);

        // Act
        var dentro = FocusSessionAccounting.ChainHolds(anterior, anterior + RuleConstants.MaxHeartbeatGap);

        // Assert
        dentro.Should().BeTrue();
    }

    [Fact]
    public void CadeiaInteira_DeveRecusarBatidaAtrasada()
    {
        // Arrange
        var anterior = Inicio + TimeSpan.FromMinutes(5);

        // Act
        var dentro = FocusSessionAccounting.ChainHolds(
            anterior, anterior + RuleConstants.MaxHeartbeatGap + TimeSpan.FromSeconds(1));

        // Assert
        dentro.Should().BeFalse();
    }
}
