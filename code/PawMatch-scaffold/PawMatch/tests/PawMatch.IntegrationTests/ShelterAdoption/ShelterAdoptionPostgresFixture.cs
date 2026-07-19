using JasperFx;
using Marten;
using PawMatch.Modules.ShelterAdoption.Api;
using Testcontainers.PostgreSql;
using Xunit;

namespace PawMatch.IntegrationTests.ShelterAdoption;

/// <summary>
/// Layer 3 (TestingApproach.md) - one real, disposable Postgres container
/// per test collection (not per test - container startup is seconds, tests
/// isolate from each other via distinct random applicant/dog-listing IDs
/// instead). Configured with the exact same ShelterAdoptionModule Marten
/// setup Api.Host uses in production, so this proves the real
/// session.Query&lt;T&gt;() LINQ paths and Marten serialization work -
/// the things Layer 2's IDocumentSession mocks can't reach.
/// </summary>
public sealed class ShelterAdoptionPostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    public IDocumentStore Store { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await _container.StartAsync();

        var module = new PawMatch.Modules.ShelterAdoption.Api.ShelterAdoptionModule();
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
public sealed class ShelterAdoptionPostgresCollection : ICollectionFixture<ShelterAdoptionPostgresFixture>
{
    public const string Name = "ShelterAdoption Postgres";
}
