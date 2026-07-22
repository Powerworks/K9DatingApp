using JasperFx;
using Marten;
using K9Crush.Modules.Chat.Api;
using Testcontainers.PostgreSql;
using Xunit;

namespace K9Crush.IntegrationTests.Chat;

/// <summary>
/// Layer 3 (TestingApproach.md) - one real, disposable Postgres container
/// per test collection, configured with the exact same ChatModule Marten
/// setup Api.Host uses in production. Every Chat handler touches either
/// AggregateStreamAsync (event-sourced command state) or session.Query
/// (the read-model projections) - neither mockable at Layer 2, same as
/// Discovery's UndoLastSwipeHandler. Mirrors DiscoveryPostgresFixture.
/// </summary>
public sealed class ChatPostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    public IDocumentStore Store { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await _container.StartAsync();

        var module = new ChatModule();
        Store = DocumentStore.For(opts =>
        {
            opts.Connection(_container.GetConnectionString());
            module.MartenConfiguration.Configure(opts);
            opts.AutoCreateSchemaObjects = AutoCreate.All;
        });
    }

    public async Task DisposeAsync()
    {
        Store.Dispose();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ChatPostgresCollection : ICollectionFixture<ChatPostgresFixture>
{
    public const string Name = "Chat Postgres";
}
