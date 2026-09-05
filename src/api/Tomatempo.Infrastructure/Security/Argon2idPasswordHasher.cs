using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Tomatempo.Infrastructure.Entities;

namespace Tomatempo.Infrastructure.Security;

/// <summary>
/// Guarda a senha com Argon2id (§7.2/A02). Hash, não criptografia: não existe chave que
/// desfaça, então dump vazado não vira lista de senhas. O custo em memória é o ponto —
/// é ele que torna a quebra offline cara mesmo com GPU.
///
/// Contas criadas antes desta troca continuam entrando: o hash PBKDF2 anterior é verificado
/// pelo hasher embutido e devolvido como <see cref="PasswordVerificationResult.SuccessRehashNeeded"/>,
/// e o Identity regrava em Argon2id no mesmo login. Ninguém precisa trocar de senha.
/// </summary>
public sealed class Argon2idPasswordHasher : IPasswordHasher<TomatempoUser>
{
    // Perfil decidido em 04/09/2026 (§7.2/A02). Mudar qualquer um destes números muda o custo
    // de cada login — e hashes antigos continuam válidos, porque os parâmetros viajam no
    // próprio texto guardado.
    private const int MemoriaEmKib = 19 * 1024;
    private const int Passagens = 2;
    private const int Paralelismo = 1;
    private const int TamanhoDoSalEmBytes = 16;
    private const int TamanhoDoHashEmBytes = 32;

    private const string Identificador = "argon2id";
    private const int VersaoArgon2 = 19;

    /// <summary>Só para verificar o que foi gravado antes da troca.</summary>
    private readonly PasswordHasher<TomatempoUser> _formatoAnterior = new();

    public string HashPassword(TomatempoUser user, string password)
    {
        var sal = RandomNumberGenerator.GetBytes(TamanhoDoSalEmBytes);
        var hash = Derivar(password, sal);

        // Formato PHC: o texto guardado se descreve sozinho, o que permite trocar parâmetros
        // no futuro sem invalidar o que já está no banco.
        return $"${Identificador}$v={VersaoArgon2}$m={MemoriaEmKib},t={Passagens},p={Paralelismo}$" +
               $"{ParaBase64Phc(sal)}${ParaBase64Phc(hash)}";
    }

    public PasswordVerificationResult VerifyHashedPassword(
        TomatempoUser user,
        string hashedPassword,
        string providedPassword)
    {
        // Senha vazia é entrada real — é o que chega quando o corpo vem sem o campo — e a
        // biblioteca de derivação não aceita chave de tamanho zero. Falha de verificação,
        // nunca exceção subindo como erro interno.
        if (string.IsNullOrEmpty(providedPassword))
        {
            return PasswordVerificationResult.Failed;
        }

        if (!hashedPassword.StartsWith($"${Identificador}$", StringComparison.Ordinal))
        {
            // Conta anterior à troca: confere no formato antigo e pede a regravação.
            return _formatoAnterior.VerifyHashedPassword(user, hashedPassword, providedPassword)
                == PasswordVerificationResult.Success
                ? PasswordVerificationResult.SuccessRehashNeeded
                : PasswordVerificationResult.Failed;
        }

        if (!TentarLer(hashedPassword, out var parametros))
        {
            // Linha corrompida ou editada à mão: falha de verificação, nunca exceção no login.
            return PasswordVerificationResult.Failed;
        }

        var esperado = parametros.Hash;
        var calculado = Derivar(
            providedPassword,
            parametros.Sal,
            parametros.MemoriaEmKib,
            parametros.Passagens,
            parametros.Paralelismo,
            esperado.Length);

        // Comparação de tempo fixo: o tempo de resposta não conta quantos bytes bateram.
        if (!CryptographicOperations.FixedTimeEquals(esperado, calculado))
        {
            return PasswordVerificationResult.Failed;
        }

        var perfilAtual = parametros.MemoriaEmKib == MemoriaEmKib
            && parametros.Passagens == Passagens
            && parametros.Paralelismo == Paralelismo;

        return perfilAtual ? PasswordVerificationResult.Success : PasswordVerificationResult.SuccessRehashNeeded;
    }

    private static byte[] Derivar(
        string password,
        byte[] sal,
        int memoriaEmKib = MemoriaEmKib,
        int passagens = Passagens,
        int paralelismo = Paralelismo,
        int tamanho = TamanhoDoHashEmBytes)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = sal,
            MemorySize = memoriaEmKib,
            Iterations = passagens,
            DegreeOfParallelism = paralelismo,
        };

        return argon.GetBytes(tamanho);
    }

    private static bool TentarLer(string phc, out (byte[] Sal, byte[] Hash, int MemoriaEmKib, int Passagens, int Paralelismo) parametros)
    {
        parametros = default;

        // $argon2id$v=19$m=19456,t=2,p=1$<sal>$<hash>
        var partes = phc.Split('$');
        if (partes.Length != 6)
        {
            return false;
        }

        var custos = partes[3].Split(',');
        if (custos.Length != 3)
        {
            return false;
        }

        if (!TentarLerCusto(custos[0], "m=", out var memoria)
            || !TentarLerCusto(custos[1], "t=", out var passagens)
            || !TentarLerCusto(custos[2], "p=", out var paralelismo))
        {
            return false;
        }

        try
        {
            parametros = (DeBase64Phc(partes[4]), DeBase64Phc(partes[5]), memoria, passagens, paralelismo);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TentarLerCusto(string parte, string prefixo, out int valor)
    {
        valor = 0;
        return parte.StartsWith(prefixo, StringComparison.Ordinal)
            && int.TryParse(parte[prefixo.Length..], out valor);
    }

    private static string ParaBase64Phc(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=');

    private static byte[] DeBase64Phc(string texto) =>
        Convert.FromBase64String(texto.PadRight((texto.Length + 3) / 4 * 4, '='));
}
