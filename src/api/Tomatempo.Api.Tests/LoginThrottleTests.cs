using Microsoft.Extensions.Time.Testing;
using Tomatempo.Api.Auth;

namespace Tomatempo.Api.Tests;

/// <summary>
/// O contador de tentativas é chaveado por e-mail vindo do cliente. Sem expurgo, quem varia o
/// endereço faz a memória do processo crescer sem teto — é entrada controlada por atacante
/// virando consumo de recurso.
/// </summary>
public sealed class LoginThrottleTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Allow_DeveDescartarJanelasVencidas_EmVezDeAcumularChaves()
    {
        // Arrange
        var relogio = new FakeTimeProvider(Agora);
        var throttle = new LoginThrottle(relogio);
        throttle.Allow("ana@example.com");
        throttle.Allow("bruno@example.com");
        throttle.PartitionCount.Should().Be(2);

        // Act — passada a janela, as chaves antigas não têm mais razão de existir.
        relogio.Advance(TimeSpan.FromMinutes(2));
        throttle.Allow("carla@example.com");

        // Assert
        throttle.PartitionCount.Should().Be(1);
    }

    [Fact]
    public void Allow_DeveManterAJanelaCorrente_QuandoAindaNaoVenceu()
    {
        // Arrange
        var relogio = new FakeTimeProvider(Agora);
        var throttle = new LoginThrottle(relogio);
        throttle.Allow("ana@example.com");

        // Act
        relogio.Advance(TimeSpan.FromSeconds(30));
        throttle.Allow("bruno@example.com");

        // Assert
        throttle.PartitionCount.Should().Be(2);
    }

    [Fact]
    public void Allow_DeveRecusarASextaTentativaDoMesmoEmail()
    {
        // Arrange
        var throttle = new LoginThrottle(new FakeTimeProvider(Agora));

        // Act
        var permitidas = Enumerable.Range(1, 6).Select(_ => throttle.Allow("ana@example.com")).ToArray();

        // Assert
        permitidas.Take(5).Should().AllBeEquivalentTo(true);
        permitidas[5].Should().BeFalse();
    }

    [Fact]
    public void Allow_DeveLiberarDeNovo_QuandoAJanelaVira()
    {
        // Arrange
        var relogio = new FakeTimeProvider(Agora);
        var throttle = new LoginThrottle(relogio);
        for (var tentativa = 1; tentativa <= 6; tentativa++)
        {
            throttle.Allow("ana@example.com");
        }

        // Act
        relogio.Advance(TimeSpan.FromMinutes(1));

        // Assert
        throttle.Allow("ana@example.com").Should().BeTrue();
    }
}
