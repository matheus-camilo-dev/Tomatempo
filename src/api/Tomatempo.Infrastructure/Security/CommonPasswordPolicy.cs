using System.Reflection;
using Tomatempo.Domain;
using Tomatempo.Domain.Ports;

namespace Tomatempo.Infrastructure.Security;

/// <summary>
/// Recusa senha curta e senha comum. A lista é um recurso embutido, carregado uma vez:
/// ampliá-la é trocar um arquivo de texto, não código (§3.10).
/// </summary>
public sealed class CommonPasswordPolicy : IPasswordPolicy
{
    private static readonly HashSet<string> Comuns = Carregar();

    public bool IsAcceptable(string? password) =>
        password is not null
        && password.Length >= RuleConstants.PasswordMinLength
        && password.Length <= RuleConstants.PasswordMaxLength
        && !Comuns.Contains(password);

    private static HashSet<string> Carregar()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Tomatempo.Infrastructure.Security.senhas-comuns.txt")
            ?? throw new InvalidOperationException("Lista de senhas comuns não foi embutida no assembly.");
        using var reader = new StreamReader(stream);

        var comuns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.ReadLine() is { } linha)
        {
            var senha = linha.Trim();
            if (senha.Length > 0 && !senha.StartsWith('#'))
            {
                comuns.Add(senha);
            }
        }

        return comuns;
    }
}
