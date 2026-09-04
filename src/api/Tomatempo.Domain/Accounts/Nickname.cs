using Tomatempo.Domain;

namespace Tomatempo.Domain.Accounts;

/// <summary>Motivo da recusa de um apelido. <see cref="None"/> significa aceito.</summary>
public enum NicknameError
{
    None,
    Missing,
    TooShort,
    TooLong,
    InvalidCharacters,
}

/// <summary>
/// Apelido do perfil público, que vira a URL <c>/u/{apelido}</c> (§3.10).
/// Só existe em forma válida e normalizada: a única porta de entrada é
/// <see cref="TryCreate"/>, que devolve o motivo da recusa em vez de lançar —
/// entrada de usuário inválida é caso esperado, não violação de invariante (§6.8).
/// </summary>
public sealed record Nickname
{
    private Nickname(string value) => Value = value;

    /// <summary>Sempre minúsculo e dentro de <c>[a-z0-9_]</c>.</summary>
    public string Value { get; }

    /// <summary>Valida e normaliza. Maiúsculas na entrada viram minúsculas (§3.10).</summary>
    public static NicknameError TryCreate(string? raw, out Nickname? nickname)
    {
        nickname = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return NicknameError.Missing;
        }

        if (raw.Length < RuleConstants.NicknameMinLength)
        {
            return NicknameError.TooShort;
        }

        if (raw.Length > RuleConstants.NicknameMaxLength)
        {
            return NicknameError.TooLong;
        }

        foreach (var caractere in raw)
        {
            var permitido = caractere is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_';
            if (!permitido)
            {
                return NicknameError.InvalidCharacters;
            }
        }

        nickname = new Nickname(raw.ToLowerInvariant());
        return NicknameError.None;
    }

    public override string ToString() => Value;
}
