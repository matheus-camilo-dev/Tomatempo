using Tomatempo.Domain.Accounts;

namespace Tomatempo.Domain.Tests.Accounts;

/// <summary>
/// Critério da §5: "Apelido fora da whitelist, curto demais, longo demais ou repetido em
/// outra caixa é recusado." A parte de duplicidade é do banco (citext) e vive no teste de
/// contrato; aqui ficam forma, tamanho e normalização (§3.10).
/// </summary>
public sealed class NicknameTests
{
    [Theory]
    [InlineData("ana")]
    [InlineData("ana_1")]
    [InlineData("a_b_c_9")]
    [InlineData("12345678901234567890")]
    public void TryCreate_DeveAceitar_QuandoTemAteVinteCaracteresDaWhitelist(string raw)
    {
        // Act
        var error = Nickname.TryCreate(raw, out var nickname);

        // Assert
        error.Should().Be(NicknameError.None);
        nickname!.Value.Should().Be(raw);
    }

    [Fact]
    public void TryCreate_DeveNormalizarParaMinusculas_QuandoEntradaTemMaiusculas()
    {
        // Act
        var error = Nickname.TryCreate("Ana_Maria", out var nickname);

        // Assert
        error.Should().Be(NicknameError.None);
        nickname!.Value.Should().Be("ana_maria");
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("a")]
    public void TryCreate_DeveRecusar_QuandoTemMenosDeTresCaracteres(string raw)
    {
        // Act
        var error = Nickname.TryCreate(raw, out var nickname);

        // Assert
        error.Should().Be(NicknameError.TooShort);
        nickname.Should().BeNull();
    }

    [Fact]
    public void TryCreate_DeveRecusar_QuandoPassaDeVinteCaracteres()
    {
        // Arrange
        var raw = new string('a', 21);

        // Act
        var error = Nickname.TryCreate(raw, out var nickname);

        // Assert
        error.Should().Be(NicknameError.TooLong);
        nickname.Should().BeNull();
    }

    [Theory]
    [InlineData("ana maria")]
    [InlineData("ana-maria")]
    [InlineData("ana.maria")]
    [InlineData("ana@casa")]
    [InlineData("aná")]
    [InlineData("ana/../raiz")]
    public void TryCreate_DeveRecusar_QuandoTemCaractereForaDaWhitelist(string raw)
    {
        // Act
        var error = Nickname.TryCreate(raw, out var nickname);

        // Assert
        error.Should().Be(NicknameError.InvalidCharacters);
        nickname.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryCreate_DeveRecusar_QuandoNaoVeioApelido(string? raw)
    {
        // Act
        var error = Nickname.TryCreate(raw, out var nickname);

        // Assert
        error.Should().Be(NicknameError.Missing);
        nickname.Should().BeNull();
    }
}
