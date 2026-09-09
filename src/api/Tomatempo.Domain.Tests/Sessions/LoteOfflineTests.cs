using Tomatempo.Domain;
using Tomatempo.Domain.Sessions;

namespace Tomatempo.Domain.Tests.Sessions;

/// <summary>
/// Aceitação de uma sessão do lote offline (§3.4). Critérios da §5: "Modo avião: 3 pomodoros
/// contam para meta/streak ao reconectar e nenhum minuto vai para o ranking" e "Reenviar o
/// mesmo lote offline não duplica nada".
///
/// O cliente manda início, duração e um identificador próprio. Nenhum desses valores decide
/// regra sozinho: cada um passa por faixa, janela e sobreposição antes de virar minuto
/// (§7.3.3).
/// </summary>
public sealed class LoteOfflineTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 9, 18, 0, 0, TimeSpan.Zero);

    private static OfflineSession Sessao(
        DateTimeOffset? inicio = null,
        int duracaoSegundos = 25 * 60,
        string id = "abc-123") =>
        new(id, inicio ?? Agora - TimeSpan.FromHours(2), TimeSpan.FromSeconds(duracaoSegundos));

    private static OfflineAdmission Julgar(
        OfflineSession sessao,
        IReadOnlyList<TimeRange>? jaRegistradas = null,
        int sessoesNoDia = 0,
        IReadOnlySet<string>? idsConhecidos = null) =>
        OfflineIntake.Judge(
            sessao,
            Agora,
            jaRegistradas ?? [],
            sessoesNoDia,
            idsConhecidos ?? new HashSet<string>());

    [Fact]
    public void Lote_DeveAceitar_QuandoASessaoEstaDentroDeTodasAsFaixas()
    {
        // Act
        var julgada = Julgar(Sessao());

        // Assert
        julgada.Accepted.Should().BeTrue();
        julgada.Rejection.Should().BeNull();
        julgada.CountedMinutes.Should().Be(25);
    }

    [Fact]
    public void Lote_DeveRecusar_QuandoOInicioPassouDeQuarentaEOitoHoras()
    {
        // Arrange — janela de 48h (§3.4): mais velho que isso o cliente descarta.
        var velha = Sessao(inicio: Agora - TimeSpan.FromHours(49));

        // Act
        var julgada = Julgar(velha);

        // Assert
        julgada.Accepted.Should().BeFalse();
        julgada.Rejection.Should().Be(OfflineRejection.TooOld);
    }

    [Fact]
    public void Lote_DeveAceitar_QuandoOInicioEncostaNaBordaDasQuarentaEOitoHoras()
    {
        // Arrange — a borda conta; quem fechou o notebook há dois dias em ponto não perde o dia.
        var naBorda = Sessao(inicio: Agora - RuleConstants.OfflineMaxAge + TimeSpan.FromMinutes(1));

        // Act
        var julgada = Julgar(naBorda);

        // Assert
        julgada.Accepted.Should().BeTrue();
    }

    [Fact]
    public void Lote_DeveRecusar_QuandoOInicioEstaNoFuturo()
    {
        // Arrange — relógio adiantado do cliente. A tolerância é de 2 minutos (§3.4).
        var adiantada = Sessao(inicio: Agora + TimeSpan.FromMinutes(10));

        // Act
        var julgada = Julgar(adiantada);

        // Assert
        julgada.Rejection.Should().Be(OfflineRejection.InFuture);
    }

    [Fact]
    public void Lote_DeveAceitar_QuandoOInicioEstaDentroDaToleranciaDeRelogio()
    {
        // Arrange — um minuto de adiantamento é desencontro de relógio, não fraude.
        var quaseAgora = Sessao(inicio: Agora + TimeSpan.FromMinutes(1) - TimeSpan.FromMinutes(25));

        // Act
        var julgada = Julgar(quaseAgora);

        // Assert
        julgada.Accepted.Should().BeTrue();
    }

    [Theory]
    [InlineData(4 * 60)]
    [InlineData(26 * 60)]
    [InlineData(0)]
    public void Lote_DeveRecusar_QuandoADuracaoEstaForaDaFaixa(int duracaoSegundos)
    {
        // Arrange — entre 5 e 25 minutos, nada além (§3.4).
        var forDaFaixa = Sessao(duracaoSegundos: duracaoSegundos);

        // Act
        var julgada = Julgar(forDaFaixa);

        // Assert
        julgada.Rejection.Should().Be(OfflineRejection.InvalidDuration);
    }

    [Fact]
    public void Lote_DeveRecusar_QuandoSobrepoeUmaSessaoJaRegistrada()
    {
        // Arrange — dois focos ao mesmo tempo não existem; é a fraude mais barata do lote.
        var inicio = Agora - TimeSpan.FromHours(2);
        var jaRegistrada = new TimeRange(inicio + TimeSpan.FromMinutes(10), inicio + TimeSpan.FromMinutes(35));

        // Act
        var julgada = Julgar(Sessao(inicio: inicio), jaRegistradas: [jaRegistrada]);

        // Assert
        julgada.Rejection.Should().Be(OfflineRejection.Overlaps);
    }

    [Fact]
    public void Lote_DeveAceitar_QuandoEncostaSemSobrepor()
    {
        // Arrange — terminar às 14h e começar às 14h é sequência, não sobreposição.
        var inicio = Agora - TimeSpan.FromHours(2);
        var anterior = new TimeRange(inicio - TimeSpan.FromMinutes(25), inicio);

        // Act
        var julgada = Julgar(Sessao(inicio: inicio), jaRegistradas: [anterior]);

        // Assert
        julgada.Accepted.Should().BeTrue();
    }

    [Fact]
    public void Lote_DeveRecusar_QuandoODiaLocalJaTemDezesseisSessoes()
    {
        // Act
        var julgada = Julgar(Sessao(), sessoesNoDia: RuleConstants.MaxSessionsPerLocalDay);

        // Assert
        julgada.Rejection.Should().Be(OfflineRejection.DailyCapReached);
    }

    [Fact]
    public void Lote_DeveAceitar_QuandoAindaCabeNoDia()
    {
        // Act
        var julgada = Julgar(Sessao(), sessoesNoDia: RuleConstants.MaxSessionsPerLocalDay - 1);

        // Assert
        julgada.Accepted.Should().BeTrue();
    }

    [Fact]
    public void Lote_DeveRecusarComoDuplicata_QuandoOIdDoClienteJaChegou()
    {
        // Arrange — reenviar o mesmo lote é no-op, não erro: a rede caindo no meio do envio
        // é o caso comum, e o cliente reenvia sem saber o que passou (§3.4).
        var conhecidos = new HashSet<string> { "abc-123" };

        // Act
        var julgada = Julgar(Sessao(id: "abc-123"), idsConhecidos: conhecidos);

        // Assert
        julgada.Accepted.Should().BeFalse();
        julgada.Rejection.Should().Be(OfflineRejection.Duplicate);
    }

    [Fact]
    public void Lote_NaoDeveValerParaORanking()
    {
        // Arrange — minuto de ranking exige começo e fim validados pelo servidor (§3.4).

        // Act
        var julgada = Julgar(Sessao());

        // Assert
        julgada.RankingEligible.Should().BeFalse();
    }

    [Fact]
    public void Lote_DeveArredondarParaBaixo_QuandoADuracaoTemSegundosQuebrados()
    {
        // Act
        var julgada = Julgar(Sessao(duracaoSegundos: 12 * 60 + 59));

        // Assert
        julgada.CountedMinutes.Should().Be(12);
    }
}
