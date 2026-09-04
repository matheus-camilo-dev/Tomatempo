namespace Tomatempo.Domain.Ports;

/// <summary>
/// Política de senha (§7.2/A07): tamanho mínimo e recusa das senhas que aparecem em toda
/// lista de vazamento. A lista é dado de infraestrutura — cresce sem tocar em regra.
/// </summary>
public interface IPasswordPolicy
{
    bool IsAcceptable(string? password);
}
