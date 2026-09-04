using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;

namespace Tomatempo.Api.Tests.Harness;

/// <summary>
/// Base dos testes de contrato: um Postgres para a suíte, uma API por classe de teste.
/// Cada teste usa e-mail e apelido próprios — é o que dispensa limpar tabela entre testes e
/// mantém a suíte paralelizável sem interferência.
/// </summary>
[Collection(ApiCollection.Name)]
public abstract class ApiFacts : IAsyncLifetime
{
    /// <summary>Senha fictícia, longa o bastante para o mínimo de 8 e fora da lista de comuns.</summary>
    protected const string SenhaValida = "tomate-tempo-9271";

    private readonly PostgresFixture _postgres;

    protected ApiFacts(PostgresFixture postgres) => _postgres = postgres;

    protected TomatempoApiFactory Api { get; private set; } = null!;

    protected RecordingEmailSender Emails => Api.Emails;

    /// <summary>Relógio do servidor. Avançar aqui é como se testa expiração sem esperar (§6.4).</summary>
    protected FakeTimeProvider Relogio => Api.Clock;

    public Task InitializeAsync()
    {
        Api = new TomatempoApiFactory(_postgres.ConnectionString);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Api.Dispose();
        return Task.CompletedTask;
    }

    protected static string NovoEmail() => $"pessoa-{Guid.NewGuid():N}@example.com";

    protected static string NovoApelido() => $"u{Guid.NewGuid():N}"[..12];

    protected HttpClient NovoCliente() => Api.NewClient();

    protected static HttpContent CadastroDe(
        string email,
        string apelido,
        string senha = SenhaValida,
        int meta = 4,
        string fuso = "America/Sao_Paulo",
        string idioma = "pt-BR") =>
        JsonContent.Create(new CadastroPayload(email, senha, apelido, meta, fuso, idioma));

    /// <summary>Cadastra alguém e devolve o cliente já com a sessão no cookie.</summary>
    protected async Task<Conta> CadastrarAsync(
        int meta = 4,
        string fuso = "America/Sao_Paulo",
        string idioma = "pt-BR",
        string senha = SenhaValida)
    {
        var email = NovoEmail();
        var apelido = NovoApelido();
        var client = NovoCliente();

        var response = await client.PostAsync("/api/auth/register", CadastroDe(email, apelido, senha, meta, fuso, idioma));
        response.IsSuccessStatusCode.Should().BeTrue("o cadastro do cenário precisa ter dado certo");

        return new Conta(client, email, apelido, senha);
    }

    protected static async Task<JsonElement> CorpoDe(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>O `code` estável do problem+json — o front traduz por ele, nunca pela mensagem (§4).</summary>
    protected static async Task<string?> CodigoDe(HttpResponseMessage response)
    {
        var body = await CorpoDe(response);
        return body.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    protected sealed record Conta(HttpClient Client, string Email, string Apelido, string Senha);

    // Payloads como record, e não objeto anônimo, para que a palavra `password` nunca apareça
    // no fonte em forma de atribuição — é o padrão que o gitleaks caça, e alargar a regra dele
    // para caber teste seria pagar em segurança o preço de uma conveniência de escrita (§7.5).
    protected sealed record CadastroPayload(
        string Email,
        string Password,
        string Nickname,
        int Goal,
        string TimeZone,
        string Locale);

    protected sealed record CredenciaisPayload(string Email, string Password);

    protected sealed record ResetPayload(string Token, string Password);
}
