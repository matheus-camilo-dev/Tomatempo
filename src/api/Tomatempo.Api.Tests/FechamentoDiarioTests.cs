using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tomatempo.Api.Services;
using Tomatempo.Api.Tests.Harness;
using Tomatempo.Domain;
using Tomatempo.Domain.Freezes;
using Tomatempo.Domain.Sessions;
using Tomatempo.Domain.Time;
using Tomatempo.Infrastructure;
using Tomatempo.Infrastructure.Entities;

namespace Tomatempo.Api.Tests;

/// <summary>
/// Fechamento do dia local (§3.6). Critérios da §5: "Baixar a meta às 23h não salva a streak
/// do dia; a nova meta aparece só amanhã", "Dia sem meta batida consome 1 freeze e mantém a
/// streak; sem freeze, zera", "7 dias consecutivos de meta batida creditam +1 freeze; dia
/// coberto por freeze reseta o contador do marco" e "Teto de 5 respeitado, com `CapOverflow`
/// registrado".
///
/// A regra pura já é testada no domínio. O que se prova aqui é o caminho: quais dias o job
/// escolhe, o que ele sela, e que reprocessar não muda nada.
/// </summary>
public sealed class FechamentoDiarioTests(PostgresFixture postgres) : ApiFacts(postgres)
{
    private async Task<(TomatempoUser User, TomatempoDbContext Db, IServiceScope Escopo)> AbrirAsync(Conta conta)
    {
        var escopo = Api.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<TomatempoDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == conta.Email);

        return (user, db, escopo);
    }

    /// <summary>Escreve um dia já vivido: tantos pomodoros concluídos, com a meta que valia.</summary>
    private static void SemearDia(TomatempoDbContext db, Guid userId, DateOnly dia, int pomodoros, int meta)
    {
        db.DailyStats.Add(new DailyStatsRow
        {
            UserId = userId,
            LocalDate = dia,
            CompletedPomodoros = pomodoros,
            FocusMinutes = pomodoros * 25,
            OnlineFocusMinutes = pomodoros * 25,
            Goal = meta,
            GoalMet = pomodoros >= meta,
        });
    }

    private async Task<int> FecharAsync()
    {
        using var escopo = Api.Services.CreateScope();
        return await escopo.ServiceProvider.GetRequiredService<DailyCloser>().CloseDueAsync(default);
    }

    /// <summary>Leva o relógio para depois da meia-noite local de São Paulo do dia seguinte.</summary>
    private void AvancarParaDepoisDaMeiaNoite() => Relogio.Advance(TimeSpan.FromHours(15));

    [Fact]
    public async Task Fechamento_DeveSelarODiaESomarNaStreak_QuandoAMetaFoiBatida()
    {
        // Arrange
        var conta = await CadastrarAsync(meta: 2);
        var (user, db, escopo) = await AbrirAsync(conta);
        using var _ = escopo;

        var hoje = LocalDay.Of(Relogio.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(user.TimeZone));
        SemearDia(db, user.Id, hoje, pomodoros: 2, meta: 2);
        await db.SaveChangesAsync();

        AvancarParaDepoisDaMeiaNoite();

        // Act
        await FecharAsync();

        // Assert
        var me = await CorpoDe(await conta.Client.GetAsync("/api/me"));
        me.GetProperty("streak").GetProperty("current").GetInt32().Should().Be(1);

        var dia = await db.DailyStats.AsNoTracking()
            .FirstAsync(d => d.UserId == user.Id && d.LocalDate == hoje);
        dia.ClosedAt.Should().NotBeNull();
        dia.GoalMet.Should().BeTrue();
    }

    [Fact]
    public async Task Fechamento_DeveConsumirCongelamento_QuandoAMetaNaoFoiBatida()
    {
        // Arrange — a conta nasce com 2 congelamentos (§3.10).
        var conta = await CadastrarAsync(meta: 4);
        var (user, db, escopo) = await AbrirAsync(conta);
        using var _ = escopo;

        var hoje = LocalDay.Of(Relogio.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(user.TimeZone));
        SemearDia(db, user.Id, hoje, pomodoros: 1, meta: 4);
        await db.SaveChangesAsync();

        AvancarParaDepoisDaMeiaNoite();

        // Act
        await FecharAsync();

        // Assert
        var me = await CorpoDe(await conta.Client.GetAsync("/api/me"));
        me.GetProperty("streak").GetProperty("current").GetInt32().Should().Be(1);
        me.GetProperty("freezes").GetProperty("balance").GetInt32()
            .Should().Be(RuleConstants.SignupFreezeGrant - 1);

        var ledger = await CorpoDe(await conta.Client.GetAsync("/api/me/freezes"));
        ledger.GetProperty("entries").EnumerateArray()
            .Should().Contain(e => e.GetProperty("reason").GetString() == nameof(FreezeReason.DayCovered));
    }

    [Fact]
    public async Task Fechamento_DeveZerarAStreak_QuandoNaoHaCongelamento()
    {
        // Arrange
        var conta = await CadastrarAsync(meta: 4);
        var (user, db, escopo) = await AbrirAsync(conta);
        using var _ = escopo;

        var streak = await db.UserStreaks.FirstAsync(s => s.UserId == user.Id);
        streak.FreezeBalance = 0;
        streak.CurrentStreak = 9;

        var hoje = LocalDay.Of(Relogio.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(user.TimeZone));
        SemearDia(db, user.Id, hoje, pomodoros: 0, meta: 4);
        await db.SaveChangesAsync();

        AvancarParaDepoisDaMeiaNoite();

        // Act
        await FecharAsync();

        // Assert
        var me = await CorpoDe(await conta.Client.GetAsync("/api/me"));
        me.GetProperty("streak").GetProperty("current").GetInt32().Should().Be(0);
        me.GetProperty("streak").GetProperty("longest").GetInt32().Should().Be(9);
    }

    [Fact]
    public async Task Fechamento_DeveCreditarOMarco_NoSetimoDiaSeguido()
    {
        // Arrange
        var conta = await CadastrarAsync(meta: 2);
        var (user, db, escopo) = await AbrirAsync(conta);
        using var _ = escopo;

        var streak = await db.UserStreaks.FirstAsync(s => s.UserId == user.Id);
        streak.CurrentStreak = 6;
        streak.ConsecutiveGoalDays = 6;

        var hoje = LocalDay.Of(Relogio.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(user.TimeZone));
        SemearDia(db, user.Id, hoje, pomodoros: 2, meta: 2);
        await db.SaveChangesAsync();

        AvancarParaDepoisDaMeiaNoite();

        // Act
        await FecharAsync();

        // Assert
        var ledger = await CorpoDe(await conta.Client.GetAsync("/api/me/freezes"));
        ledger.GetProperty("balance").GetInt32().Should().Be(RuleConstants.SignupFreezeGrant + 1);
        ledger.GetProperty("entries").EnumerateArray()
            .Should().Contain(e => e.GetProperty("reason").GetString() == nameof(FreezeReason.StreakMilestone));
    }

    [Fact]
    public async Task Fechamento_DeveRegistrarCapOverflow_QuandoOEstoqueEstaNoTeto()
    {
        // Arrange
        var conta = await CadastrarAsync(meta: 2);
        var (user, db, escopo) = await AbrirAsync(conta);
        using var _ = escopo;

        var streak = await db.UserStreaks.FirstAsync(s => s.UserId == user.Id);
        streak.CurrentStreak = 6;
        streak.ConsecutiveGoalDays = 6;
        streak.FreezeBalance = RuleConstants.FreezeStockCap;

        var hoje = LocalDay.Of(Relogio.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(user.TimeZone));
        SemearDia(db, user.Id, hoje, pomodoros: 2, meta: 2);
        await db.SaveChangesAsync();

        AvancarParaDepoisDaMeiaNoite();

        // Act
        await FecharAsync();

        // Assert
        var ledger = await CorpoDe(await conta.Client.GetAsync("/api/me/freezes"));
        ledger.GetProperty("balance").GetInt32().Should().Be(RuleConstants.FreezeStockCap);
        ledger.GetProperty("entries").EnumerateArray()
            .Should().Contain(e => e.GetProperty("reason").GetString() == nameof(FreezeReason.CapOverflow));
    }

    [Fact]
    public async Task Fechamento_NaoDeveMudarNada_QuandoODiaJaFoiFechado()
    {
        // Arrange — idempotência (§3.6): reprocessar não pode somar streak duas vezes.
        var conta = await CadastrarAsync(meta: 2);
        var (user, db, escopo) = await AbrirAsync(conta);
        using var _ = escopo;

        var hoje = LocalDay.Of(Relogio.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(user.TimeZone));
        SemearDia(db, user.Id, hoje, pomodoros: 2, meta: 2);
        await db.SaveChangesAsync();

        AvancarParaDepoisDaMeiaNoite();
        await FecharAsync();

        // Act
        var segundaVez = await FecharAsync();

        // Assert
        segundaVez.Should().Be(0);
        var me = await CorpoDe(await conta.Client.GetAsync("/api/me"));
        me.GetProperty("streak").GetProperty("current").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Fechamento_NaoDeveTocarNoDiaQueAindaEstaEmCurso()
    {
        // Arrange — meio-dia local: o dia de hoje não fecha.
        var conta = await CadastrarAsync(meta: 2);
        var (user, db, escopo) = await AbrirAsync(conta);
        using var _ = escopo;

        var hoje = LocalDay.Of(Relogio.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(user.TimeZone));
        SemearDia(db, user.Id, hoje, pomodoros: 2, meta: 2);
        await db.SaveChangesAsync();

        // Act
        var fechados = await FecharAsync();

        // Assert
        fechados.Should().Be(0);
        var me = await CorpoDe(await conta.Client.GetAsync("/api/me"));
        me.GetProperty("streak").GetProperty("current").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Fechamento_DeveCobrarAMetaQueValiaNoDia_MesmoQueOUsuarioABaixeDepois()
    {
        // Arrange — critério da §5: baixar a meta às 23h não salva a streak do dia. A edição
        // vale a partir de amanhã (§3.5), e o dia fecha contra a meta que valia nele.
        var conta = await CadastrarAsync(meta: 4);
        var (user, db, escopo) = await AbrirAsync(conta);
        using var _ = escopo;

        var streak = await db.UserStreaks.FirstAsync(s => s.UserId == user.Id);
        streak.FreezeBalance = 0;

        var hoje = LocalDay.Of(Relogio.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(user.TimeZone));
        SemearDia(db, user.Id, hoje, pomodoros: 2, meta: 4);
        await db.SaveChangesAsync();

        // Act — às 23h local, baixa a meta para 2 e espera o dia virar.
        await conta.Client.PutAsJsonAsync("/api/me/goal", new { pomodoros = 2 });
        AvancarParaDepoisDaMeiaNoite();
        await FecharAsync();

        // Assert
        var me = await CorpoDe(await conta.Client.GetAsync("/api/me"));
        me.GetProperty("streak").GetProperty("current").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Fechamento_DeveFecharTodosOsDiasEmAberto_QuandoOJobFicouForaDoAr()
    {
        // Arrange — três dias vividos e nenhum fechado; nenhum pode ser pulado (§3.6).
        var conta = await CadastrarAsync(meta: 2);
        var (user, db, escopo) = await AbrirAsync(conta);
        using var _ = escopo;

        var fuso = TimeZoneInfo.FindSystemTimeZoneById(user.TimeZone);
        var hoje = LocalDay.Of(Relogio.GetUtcNow(), fuso);
        for (var i = 0; i < 3; i++)
        {
            SemearDia(db, user.Id, hoje.AddDays(-i), pomodoros: 2, meta: 2);
        }

        var streak = await db.UserStreaks.FirstAsync(s => s.UserId == user.Id);
        streak.LastClosedLocalDate = hoje.AddDays(-3);
        await db.SaveChangesAsync();

        AvancarParaDepoisDaMeiaNoite();

        // Act
        await FecharAsync();

        // Assert
        var me = await CorpoDe(await conta.Client.GetAsync("/api/me"));
        me.GetProperty("streak").GetProperty("current").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task Fechamento_DeveContarDiaSemAtividadeAlguma()
    {
        // Arrange — dia parado também consome congelamento; não existe "dia que não conta"
        // (REGRAS-DE-NEGOCIO.md §4). Nem linha de `daily_stats` esse dia tem.
        var conta = await CadastrarAsync(meta: 4);
        var (user, db, escopo) = await AbrirAsync(conta);
        using var _ = escopo;

        var streak = await db.UserStreaks.FirstAsync(s => s.UserId == user.Id);
        streak.CurrentStreak = 5;
        await db.SaveChangesAsync();

        AvancarParaDepoisDaMeiaNoite();

        // Act
        await FecharAsync();

        // Assert
        var me = await CorpoDe(await conta.Client.GetAsync("/api/me"));
        me.GetProperty("streak").GetProperty("current").GetInt32().Should().Be(6);
        me.GetProperty("freezes").GetProperty("balance").GetInt32()
            .Should().Be(RuleConstants.SignupFreezeGrant - 1);
    }

    [Fact]
    public async Task Fechamento_NaoDeveMisturarUsuarios()
    {
        // Arrange — o fechamento de uma conta não pode encostar na streak de outra.
        var batida = await CadastrarAsync(meta: 2);
        var parada = await CadastrarAsync(meta: 4);

        var (userBatida, db, escopo) = await AbrirAsync(batida);
        using var _ = escopo;

        var hoje = LocalDay.Of(Relogio.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(userBatida.TimeZone));
        SemearDia(db, userBatida.Id, hoje, pomodoros: 2, meta: 2);
        await db.SaveChangesAsync();

        AvancarParaDepoisDaMeiaNoite();

        // Act
        await FecharAsync();

        // Assert — quem bateu soma; quem parou consome congelamento, e cada uma com o seu.
        var meBatida = await CorpoDe(await batida.Client.GetAsync("/api/me"));
        meBatida.GetProperty("streak").GetProperty("current").GetInt32().Should().Be(1);
        meBatida.GetProperty("freezes").GetProperty("balance").GetInt32()
            .Should().Be(RuleConstants.SignupFreezeGrant);

        var meParada = await CorpoDe(await parada.Client.GetAsync("/api/me"));
        meParada.GetProperty("freezes").GetProperty("balance").GetInt32()
            .Should().Be(RuleConstants.SignupFreezeGrant - 1);
    }

    [Fact]
    public async Task Fechamento_DeveSelarOsMinutosDaSessaoQueOVarredorEncerrou()
    {
        // Arrange — a sessão órfã vira parcial antes do dia fechar, e o parcial entra no dia.
        var conta = await CadastrarAsync(meta: 2);
        var inicio = await CorpoDe(
            await conta.Client.PostAsJsonAsync("/api/sessions/start", new { kind = "focus" }));
        var id = inicio.GetProperty("sessionId").GetGuid();

        for (var i = 0; i < 10; i++)
        {
            Relogio.Advance(TimeSpan.FromSeconds(60));
            await conta.Client.PostAsync($"/api/sessions/{id}/beat", content: null);
        }

        Relogio.Advance(RuleConstants.ScavengerIdleLimit + TimeSpan.FromSeconds(30));

        using var escopo = Api.Services.CreateScope();
        await escopo.ServiceProvider.GetRequiredService<SessionScavenger>().SweepAsync(default);

        AvancarParaDepoisDaMeiaNoite();

        // Act
        await FecharAsync();

        // Assert
        var db = escopo.ServiceProvider.GetRequiredService<TomatempoDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == conta.Email);
        var hoje = LocalDay.Of(
            TomatempoApiFactory.InicioDoTeste, TimeZoneInfo.FindSystemTimeZoneById(user.TimeZone));

        var dia = await db.DailyStats.AsNoTracking()
            .FirstAsync(d => d.UserId == user.Id && d.LocalDate == hoje);
        dia.FocusMinutes.Should().Be(10);
        dia.ClosedAt.Should().NotBeNull();
        dia.GoalMet.Should().BeFalse();
    }

    [Fact]
    public async Task Sessao_NaoDeveEntrarEmDiaJaSelado()
    {
        // Arrange — a streak do dia já foi decidida; sessão que chega depois é histórico (§3.6).
        var conta = await CadastrarAsync(meta: 2);
        var (user, db, escopo) = await AbrirAsync(conta);
        using var _ = escopo;

        var hoje = LocalDay.Of(Relogio.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(user.TimeZone));
        SemearDia(db, user.Id, hoje, pomodoros: 0, meta: 2);
        var dia = await db.DailyStats.FirstAsync(d => d.UserId == user.Id && d.LocalDate == hoje);
        dia.ClosedAt = Relogio.GetUtcNow();
        await db.SaveChangesAsync();

        // Act — uma sessão inteira apurada no mesmo dia local, depois do selo.
        var inicio = await CorpoDe(
            await conta.Client.PostAsJsonAsync("/api/sessions/start", new { kind = "focus" }));
        var id = inicio.GetProperty("sessionId").GetGuid();
        for (var i = 0; i < 10; i++)
        {
            Relogio.Advance(TimeSpan.FromSeconds(60));
            await conta.Client.PostAsync($"/api/sessions/{id}/beat", content: null);
        }

        await conta.Client.PostAsync($"/api/sessions/{id}/abandon", content: null);

        // Assert
        var selado = await db.DailyStats.AsNoTracking()
            .FirstAsync(d => d.UserId == user.Id && d.LocalDate == hoje);
        selado.FocusMinutes.Should().Be(0);
    }
}
