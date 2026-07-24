using JasperFx;
using Marten;
using K9Crush.Modules.Media.Api;
using Testcontainers.PostgreSql;
using Xunit;

namespace K9Crush.IntegrationTests.Media;

/// <summary>
/// Layer 3 (TestingApproach.md) - one real, disposable Postgres container
/// per test collection, configured with the exact same MediaModule Marten
/// setup Api.Host uses in production. Media has no session.Query&lt;T&gt;()
/// read models today, so this fixture exists purely for ADR-031's Phase 1
/// spike: confirming FetchForWriting/AggregateStreamAsync's real runtime
/// behavior against a nonexistent stream (null vs. throw), which no mock
/// or compile-time check can answer. Mirrors AdminPostgresFixture/etc.
/// </summary>
public sealed class MediaPostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    public IDocumentStore Store { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await _container.StartAsync();

        var module = new MediaModule();
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
public sealed class MediaPostgresCollection : ICollectionFixture<MediaPostgresFixture>
{
    public const string Name = "Media Postgres";
}
