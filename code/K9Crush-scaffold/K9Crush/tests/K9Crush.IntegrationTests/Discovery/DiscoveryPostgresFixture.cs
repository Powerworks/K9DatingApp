using JasperFx;
using Marten;
using K9Crush.Modules.Discovery.Api;
using Testcontainers.PostgreSql;
using Xunit;

namespace K9Crush.IntegrationTests.Discovery;

/// <summary>
/// Layer 3 (TestingApproach.md) - one real, disposable Postgres container
/// per test collection, configured with the exact same DiscoveryModule
/// Marten setup Api.Host uses in production. Needed (rather than a Layer 2
/// mock) for anything exercising session.Events.Append/AggregateStreamAsync -
/// Marten's real event store, not something NSubstitute can meaningfully
/// fake. Mirrors ShelterAdoptionPostgresFixture.
/// </summary>
public sealed class DiscoveryPostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    public IDocumentStore Store { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await _container.StartAsync();

        var module = new DiscoveryModule();
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
public sealed class DiscoveryPostgresCollection : ICollectionFixture<DiscoveryPostgresFixture>
{
    public const string Name = "Discovery Postgres";
}
