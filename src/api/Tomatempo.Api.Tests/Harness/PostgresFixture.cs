using Testcontainers.PostgreSql;

namespace Tomatempo.Api.Tests.Harness;

/// <summary>
/// Postgres de verdade para a suíte inteira. O provider InMemory é proibido (§6.4): ele
/// esconde justamente os erros de SQL, de índice único e de `citext` que estes testes
/// existem para pegar.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "api";
}
