using Tomatempo.Domain;
using Tomatempo.Domain.Freezes;
using Tomatempo.Domain.Streaks;

namespace Tomatempo.Domain.Tests.Streaks;

/// <summary>
/// Fechamento de um dia local (§3.6). Critérios da §5: "Dia sem meta batida consome 1 freeze e
/// mantém a streak; sem freeze, zera", "7 dias consecutivos de meta batida creditam +1 freeze;
/// dia coberto por freeze reseta o contador do marco" e "Teto de 5 respeitado, com
/// `CapOverflow` registrado".
///
/// Função pura: o dia local já chega resolvido e a meta já chega comparada. Esta camada não
/// conhece fuso, relógio nem banco.
/// </summary>
public sealed class FechamentoDoDiaTests
{
    private static readonly DateOnly Dia = new(2026, 9, 9);
    private static readonly DateTimeOffset Agora = new(2026, 9, 10, 3, 0, 0, TimeSpan.Zero);

    private static StreakState Estado(
        int atual = 0,
        int maisLonga = 0,
        int consecutivos = 0,
        int congelamentos = 0) =>
        new(atual, maisLonga, consecutivos, congelamentos);

    [Fact]
    public void Fechamento_DeveSomarUmNaStreak_QuandoAMetaFoiBatida()
    {
        // Arrange
        var antes = Estado(atual: 3, maisLonga: 5, consecutivos: 3);

        // Act
        var fechado = DayClosing.Close(antes, goalMet: true, Dia, Agora);

        // Assert
        fechado.State.CurrentStreak.Should().Be(4);
        fechado.State.ConsecutiveGoalDays.Should().Be(4);
        fechado.Ledger.Should().BeEmpty();
    }

    [Fact]
    public void Fechamento_DeveConsumirUmCongelamento_QuandoAMetaNaoFoiBatida()
    {
        // Arrange
        var antes = Estado(atual: 3, maisLonga: 5, consecutivos: 3, congelamentos: 2);

        // Act
        var fechado = DayClosing.Close(antes, goalMet: false, Dia, Agora);

        // Assert — a streak sobrevive e o saldo cai um.
        fechado.State.CurrentStreak.Should().Be(4);
        fechado.State.FreezeBalance.Should().Be(1);
        fechado.Ledger.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new FreezeLedgerEntry(-1, FreezeReason.DayCovered, Dia, Agora));
    }

    [Fact]
    public void Fechamento_DeveResetarOContadorDoMarco_QuandoODiaFoiCobertoPorCongelamento()
    {
        // Arrange — senão dá para farmar congelamento com congelamento (§3.6).
        var antes = Estado(atual: 6, consecutivos: 6, congelamentos: 2);

        // Act
        var fechado = DayClosing.Close(antes, goalMet: false, Dia, Agora);

        // Assert
        fechado.State.ConsecutiveGoalDays.Should().Be(0);
        fechado.Ledger.Should().NotContain(linha => linha.Reason == FreezeReason.StreakMilestone);
    }

    [Fact]
    public void Fechamento_DeveZerarAStreak_QuandoNaoHaCongelamentoParaCobrir()
    {
        // Arrange
        var antes = Estado(atual: 9, maisLonga: 9, consecutivos: 2, congelamentos: 0);

        // Act
        var fechado = DayClosing.Close(antes, goalMet: false, Dia, Agora);

        // Assert
        fechado.State.CurrentStreak.Should().Be(0);
        fechado.State.ConsecutiveGoalDays.Should().Be(0);
        fechado.State.LongestStreak.Should().Be(9);
        fechado.Ledger.Should().BeEmpty();
    }

    [Fact]
    public void Fechamento_DeveCreditarUmCongelamento_QuandoFechaSeteDiasSeguidos()
    {
        // Arrange
        var antes = Estado(atual: 6, consecutivos: 6, congelamentos: 1);

        // Act
        var fechado = DayClosing.Close(antes, goalMet: true, Dia, Agora);

        // Assert
        fechado.State.FreezeBalance.Should().Be(2);
        fechado.Ledger.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new FreezeLedgerEntry(1, FreezeReason.StreakMilestone, Dia, Agora));
    }

    [Fact]
    public void Fechamento_DeveCreditarDeNovo_QuandoFechaQuatorzeDiasSeguidos()
    {
        // Arrange — o marco é a cada 7, não só o primeiro.
        var antes = Estado(atual: 13, consecutivos: 13, congelamentos: 1);

        // Act
        var fechado = DayClosing.Close(antes, goalMet: true, Dia, Agora);

        // Assert
        fechado.State.FreezeBalance.Should().Be(2);
        fechado.Ledger.Should().ContainSingle().Which.Reason.Should().Be(FreezeReason.StreakMilestone);
    }

    [Fact]
    public void Fechamento_NaoDeveCreditar_QuandoOMarcoAindaNaoChegou()
    {
        // Arrange
        var antes = Estado(atual: 4, consecutivos: 4, congelamentos: 1);

        // Act
        var fechado = DayClosing.Close(antes, goalMet: true, Dia, Agora);

        // Assert
        fechado.State.FreezeBalance.Should().Be(1);
        fechado.Ledger.Should().BeEmpty();
    }

    [Fact]
    public void Fechamento_DeveRegistrarCapOverflow_QuandoOMarcoChegaComOEstoqueNoTeto()
    {
        // Arrange — o crédito não some em silêncio: fica no ledger com delta zero (§3.6).
        var antes = Estado(atual: 6, consecutivos: 6, congelamentos: RuleConstants.FreezeStockCap);

        // Act
        var fechado = DayClosing.Close(antes, goalMet: true, Dia, Agora);

        // Assert
        fechado.State.FreezeBalance.Should().Be(RuleConstants.FreezeStockCap);
        fechado.Ledger.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new FreezeLedgerEntry(0, FreezeReason.CapOverflow, Dia, Agora));
    }

    [Fact]
    public void Fechamento_DeveAtualizarAMaisLonga_QuandoAStreakCorrentePassaDela()
    {
        // Arrange
        var antes = Estado(atual: 9, maisLonga: 9, consecutivos: 2);

        // Act
        var fechado = DayClosing.Close(antes, goalMet: true, Dia, Agora);

        // Assert
        fechado.State.LongestStreak.Should().Be(10);
    }

    [Fact]
    public void Fechamento_DeveManterAMaisLonga_QuandoACorrenteAindaNaoAAlcancou()
    {
        // Arrange
        var antes = Estado(atual: 2, maisLonga: 30, consecutivos: 2);

        // Act
        var fechado = DayClosing.Close(antes, goalMet: true, Dia, Agora);

        // Assert
        fechado.State.LongestStreak.Should().Be(30);
    }

    [Fact]
    public void Fechamento_DeveSobreviverAUmDiaSemAtividadeAlguma()
    {
        // Arrange — dia parado também consome congelamento (REGRAS-DE-NEGOCIO.md §4).
        var antes = Estado(atual: 12, maisLonga: 12, consecutivos: 5, congelamentos: 3);

        // Act
        var fechado = DayClosing.Close(antes, goalMet: false, Dia, Agora);

        // Assert
        fechado.State.CurrentStreak.Should().Be(13);
        fechado.State.FreezeBalance.Should().Be(2);
    }
}
