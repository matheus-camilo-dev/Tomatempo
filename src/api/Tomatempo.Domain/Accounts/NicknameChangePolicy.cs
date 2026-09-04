namespace Tomatempo.Domain.Accounts;

/// <summary>
/// Trava de 30 dias entre trocas de apelido (§11.8): o apelido é a identidade pública
/// no ranking, e rodízio de identidade atrapalha quem acompanha o perfil de alguém.
/// </summary>
public static class NicknameChangePolicy
{
    /// <param name="lastChangedAt"><c>null</c> quando o apelido nunca foi trocado desde o cadastro.</param>
    public static bool CanChange(DateTimeOffset? lastChangedAt, DateTimeOffset now) =>
        throw new NotImplementedException();

    /// <summary>Instante em que a próxima troca passa a ser permitida.</summary>
    public static DateTimeOffset NextChangeAllowedAt(DateTimeOffset lastChangedAt) =>
        throw new NotImplementedException();
}
