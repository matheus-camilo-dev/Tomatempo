namespace Tomatempo.Domain.Accounts;

/// <summary>
/// Como um e-mail aparece no log de eventos de autenticação (§7.2/A09). Guardar o endereço
/// inteiro transformaria o log em uma lista de quem usa o site — o mesmo dado que a resposta
/// da API se recusa a revelar. Fica a primeira letra, para dar rastro, e o domínio, que ajuda
/// a reconhecer um ataque vindo de um provedor só.
/// </summary>
public static class AuditMasking
{
    private const string Marcador = "***";

    /// <summary>Máscara de tamanho fixo: nem o comprimento do nome vaza.</summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Marcador;
        }

        var arroba = email.IndexOf('@', StringComparison.Ordinal);

        // Sem arroba, ou com a parte local vazia, não dá para dizer o que é nome e o que é
        // domínio — e chutar seria arriscar deixar passar o endereço inteiro.
        if (arroba <= 0 || arroba == email.Length - 1)
        {
            return Marcador;
        }

        return $"{email[0]}{Marcador}{email[arroba..]}";
    }
}
