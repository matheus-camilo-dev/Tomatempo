using Tomatempo.Domain.Accounts;

namespace Tomatempo.Domain.Tests.Accounts;

/// <summary>
/// §7.2/A09: o log de autenticação não carrega e-mail completo. Esta é a única parte da
/// regra que dá para afirmar em teste — que o endereço nunca sai inteiro.
/// </summary>
public sealed class AuditMaskingTests
{
    [Fact]
    public void MaskEmail_DeveGuardarSoAPrimeiraLetraEODominio()
    {
        // Act
        var mascarado = AuditMasking.MaskEmail("ana.maria@example.com");

        // Assert
        mascarado.Should().Be("a***@example.com");
    }

    [Fact]
    public void MaskEmail_NaoDeveVazarOTamanhoDoNome()
    {
        // Arrange — nomes de tamanhos bem diferentes, mesmo domínio.
        var curto = AuditMasking.MaskEmail("ab@example.com");
        var longo = AuditMasking.MaskEmail("abcdefghijklmnop@example.com");

        // Assert
        longo.Should().Be(curto);
    }

    [Fact]
    public void MaskEmail_DeveMascararTudo_QuandoNaoParecerEmail()
    {
        // Act
        var mascarado = AuditMasking.MaskEmail("isto-nao-e-um-email");

        // Assert
        mascarado.Should().Be("***");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MaskEmail_DeveDevolverMarcador_QuandoNaoVeioEmail(string? email)
    {
        // Act
        var mascarado = AuditMasking.MaskEmail(email);

        // Assert
        mascarado.Should().Be("***");
    }

    [Fact]
    public void MaskEmail_DeveMascararTambemOArrobaSolto()
    {
        // Arrange — entrada malformada não pode virar exceção no meio de um log.

        // Act
        var mascarado = AuditMasking.MaskEmail("@example.com");

        // Assert
        mascarado.Should().Be("***");
    }
}
