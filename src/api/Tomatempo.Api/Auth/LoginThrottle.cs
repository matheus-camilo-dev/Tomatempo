using System.Collections.Concurrent;
using Tomatempo.Domain;

namespace Tomatempo.Api.Auth;

/// <summary>
/// Limite de 5 tentativas por minuto em login e recuperação de senha (§4), particionado por
/// e-mail e não por IP: o alvo do ataque é uma conta específica, e a partição por e-mail não
/// revela nada — a resposta continua idêntica para conta existente e inexistente (§3.10).
///
/// O relógio é injetado, então o teste controla a janela sem esperar (§6.4).
/// </summary>
public sealed class LoginThrottle(TimeProvider clock)
{
    private static readonly TimeSpan Janela = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, Contador> _tentativas = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Quantas chaves o contador guarda agora — o que o expurgo mantém sob controle.</summary>
    public int PartitionCount => _tentativas.Count;

    public bool Allow(string? email)
    {
        var chave = email?.Trim() ?? string.Empty;
        var agora = clock.GetUtcNow();

        Expurgar(agora);

        var contador = _tentativas.AddOrUpdate(
            chave,
            _ => new Contador(agora, 1),
            (_, atual) => agora - atual.InicioDaJanela >= Janela
                ? new Contador(agora, 1)
                : atual with { Tentativas = atual.Tentativas + 1 });

        return contador.Tentativas <= RuleConstants.AuthAttemptsPerMinute;
    }

    /// <summary>
    /// Descarta as janelas vencidas. Sem isso o dicionário guarda uma chave por e-mail já
    /// tentado, e o e-mail vem do cliente — memória do processo crescendo por entrada de
    /// atacante. A varredura é proporcional ao que está vivo, que é o tráfego de um minuto.
    /// </summary>
    private void Expurgar(DateTimeOffset agora)
    {
        foreach (var (chave, contador) in _tentativas)
        {
            if (agora - contador.InicioDaJanela >= Janela)
            {
                _tentativas.TryRemove(chave, out _);
            }
        }
    }

    private sealed record Contador(DateTimeOffset InicioDaJanela, int Tentativas);
}
