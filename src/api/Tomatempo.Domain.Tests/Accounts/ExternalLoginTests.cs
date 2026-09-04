using Tomatempo.Domain.Accounts;

namespace Tomatempo.Domain.Tests.Accounts;

/// <summary>
/// Critério da §5: "Google com e-mail já cadastrado entra na conta existente; sem
/// `email_verified` do provedor, é recusado."
/// </summary>
public sealed class ExternalLoginTests
{
    private static ExternalIdentity Identidade(bool emailVerified = true) =>
        new("Google", Subject: "sub-123", Email: "pessoa@example.com", EmailVerified: emailVerified);

    [Fact]
    public void Decide_DeveEntrarNaConta_QuandoOProvedorJaEstaVinculado()
    {
        // Act
        var decision = ExternalLogin.Decide(Identidade(), providerAlreadyLinked: true, emailAlreadyRegistered: true);

        // Assert
        decision.Should().Be(ExternalLoginDecision.SignInLinked);
    }

    [Fact]
    public void Decide_DeveVincularAContaExistente_QuandoOEmailJaTemCadastroDeSenha()
    {
        // Act
        var decision = ExternalLogin.Decide(Identidade(), providerAlreadyLinked: false, emailAlreadyRegistered: true);

        // Assert
        decision.Should().Be(ExternalLoginDecision.LinkToExistingAccount);
    }

    [Fact]
    public void Decide_DeveCriarConta_QuandoOEmailENovo()
    {
        // Act
        var decision = ExternalLogin.Decide(Identidade(), providerAlreadyLinked: false, emailAlreadyRegistered: false);

        // Assert
        decision.Should().Be(ExternalLoginDecision.CreateAccount);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Decide_DeveRecusar_QuandoOProvedorNaoConfirmaOEmail(bool providerAlreadyLinked, bool emailAlreadyRegistered)
    {
        // Arrange — sem e-mail verificado, aceitar seria deixar qualquer um dizer que é dono
        // do endereço alheio; vale mesmo quando o provedor já está vinculado.
        var identity = Identidade(emailVerified: false);

        // Act
        var decision = ExternalLogin.Decide(identity, providerAlreadyLinked, emailAlreadyRegistered);

        // Assert
        decision.Should().Be(ExternalLoginDecision.RejectUnverifiedEmail);
    }
}
