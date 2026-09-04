using Tomatempo.Domain.Accounts;

namespace Tomatempo.Api.Auth;

/// <summary>
/// Log estruturado dos eventos de autenticação (§7.2/A09). Existe como classe própria, e não
/// como chamadas soltas de logger, porque a regra que importa é uma só e precisa valer em
/// todos os pontos: **o e-mail entra mascarado, a senha e o token não entram**. Concentrar
/// aqui é o que impede alguém de acrescentar um `LogInformation` com o endereço inteiro.
/// </summary>
public sealed class AuthAudit(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("Tomatempo.Autenticacao");

    public void ContaCriada(string? email) =>
        _logger.LogInformation("Conta criada para {Email}.", AuditMasking.MaskEmail(email));

    public void CadastroComEmailJaUsado(string? email) =>
        _logger.LogInformation("Cadastro recusado: {Email} já tem conta.", AuditMasking.MaskEmail(email));

    public void LoginBemSucedido(string? email) =>
        _logger.LogInformation("Login aceito para {Email}.", AuditMasking.MaskEmail(email));

    public void LoginFalhou(string? email) =>
        _logger.LogWarning("Login recusado para {Email}.", AuditMasking.MaskEmail(email));

    public void LimiteAtingido(string operacao, string? email) =>
        _logger.LogWarning(
            "Limite de tentativas atingido em {Operacao} para {Email}.", operacao, AuditMasking.MaskEmail(email));

    public void EmailConfirmado(string? email) =>
        _logger.LogInformation("E-mail confirmado para {Email}.", AuditMasking.MaskEmail(email));

    public void SenhaRedefinida(string? email) =>
        _logger.LogInformation("Senha redefinida para {Email}; sessões anteriores derrubadas.",
            AuditMasking.MaskEmail(email));

    /// <summary>Sem e-mail: o token recusado não diz de quem era, e não vamos descobrir para logar.</summary>
    public void TokenRecusado(string operacao) =>
        _logger.LogWarning("Token recusado em {Operacao}.", operacao);

    public void LoginExternoRecusado(string? email) =>
        _logger.LogWarning("Login externo recusado (e-mail não verificado pelo provedor) para {Email}.",
            AuditMasking.MaskEmail(email));

    public void ContaExternaVinculada(string? email) =>
        _logger.LogInformation("Provedor externo vinculado à conta de {Email}.", AuditMasking.MaskEmail(email));
}
