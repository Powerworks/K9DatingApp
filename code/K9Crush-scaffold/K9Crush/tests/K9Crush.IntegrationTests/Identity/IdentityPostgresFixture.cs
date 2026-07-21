using JasperFx;
using Marten;
using K9Crush.Modules.Identity.Api;
using Testcontainers.PostgreSql;
using Xunit;

namespace K9Crush.IntegrationTests.Identity;

/// <summary>
/// Layer 3 (TestingApproach.md) - one real, disposable Postgres container
/// per test collection, configured with the exact same IdentityModule
/// Marten setup Api.Host uses in production. Needed for
/// BootstrapAdminHandler's "does any Admin already exist" check
/// (session.Query&lt;OwnerAccount&gt;().AnyAsync) - the LINQ path Layer 2's
/// IDocumentSession mocks can't reach. Mirrors ShelterAdoptionPostgresFixture/
/// DiscoveryPostgresFixture/ProfilesPostgresFixture.
/// </summary>
public sealed class IdentityPostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    public IDocumentStore Store { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await _container.StartAsync();

        var module = new IdentityModule();
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
public sealed class IdentityPostgresCollection : ICollectionFixture<IdentityPostgresFixture>
{
    public const string Name = "Identity Postgres";
}
