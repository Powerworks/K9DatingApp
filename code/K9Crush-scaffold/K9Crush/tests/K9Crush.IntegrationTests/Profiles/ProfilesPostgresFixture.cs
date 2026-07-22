using JasperFx;
using Marten;
using K9Crush.Modules.Profiles.Api;
using Testcontainers.PostgreSql;
using Xunit;

namespace K9Crush.IntegrationTests.Profiles;

/// <summary>
/// Layer 3 (TestingApproach.md) - one real, disposable Postgres container
/// per test collection, configured with the exact same ProfilesModule
/// Marten setup Api.Host uses in production. Needed for
/// StartDogProfileHandler's maxDogProfiles cap, which calls
/// session.Query&lt;DogProfile&gt;() - the LINQ path Layer 2's
/// IDocumentSession mocks can't reach. Mirrors ShelterAdoptionPostgresFixture/
/// DiscoveryPostgresFixture.
/// </summary>
public sealed class ProfilesPostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    public IDocumentStore Store { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await _container.StartAsync();

        var module = new ProfilesModule();
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
public sealed class ProfilesPostgresCollection : ICollectionFixture<ProfilesPostgresFixture>
{
    public const string Name = "Profiles Postgres";
}
