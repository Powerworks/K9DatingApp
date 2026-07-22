using JasperFx;
using Marten;
using K9Crush.Modules.Moderation.Api;
using Testcontainers.PostgreSql;
using Xunit;

namespace K9Crush.IntegrationTests.Moderation;

/// <summary>
/// Layer 3 (TestingApproach.md) - one real, disposable Postgres container
/// per test collection, configured with the exact same ModerationModule
/// Marten setup Api.Host uses in production. Needed for
/// GetModerationQueueHandler's session.Query&lt;FlaggedContent&gt;() - the
/// LINQ path Layer 2's IDocumentSession mocks can't reach. Mirrors
/// AdminPostgresFixture/NotificationsPostgresFixture/etc.
/// </summary>
public sealed class ModerationPostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    public IDocumentStore Store { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await _container.StartAsync();

        var module = new ModerationModule();
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
