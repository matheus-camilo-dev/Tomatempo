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
    public static string MaskEmail(string? email) => throw new NotImplementedException();
}
