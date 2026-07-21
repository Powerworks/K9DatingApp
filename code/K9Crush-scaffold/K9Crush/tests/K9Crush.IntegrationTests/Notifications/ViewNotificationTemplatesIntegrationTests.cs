using FluentAssertions;
using K9Crush.Modules.Notifications.Api.ReadModels.ViewNotificationTemplates;
using K9Crush.Modules.Notifications.Domain;
using Xunit;

namespace K9Crush.IntegrationTests.Notifications;

/// <summary>
/// Layer 3 (TestingApproach.md) - ViewNotificationTemplatesHandler calls
/// session.Query&lt;NotificationTemplate&gt;().ToListAsync(), the LINQ
/// path Layer 2's IDocumentSession mocks can't reach.
///
/// Deliberately does NOT share NotificationsPostgresFixture via
/// [Collection(...)]/ICollectionFixture - this handler's query is
/// genuinely unscoped (returns every template, no per-test filter like
/// DogId/OwnerId elsewhere in this codebase), so an "empty list" test
/// sharing a container with any test that seeds templates would be
/// order-dependent (confirmed live - this is exactly what happened on
/// the first pass). Same per-test-instance IAsyncLifetime fix as
/// BootstrapAdminIntegrationTests.
/// </summary>
public class ViewNotificationTemplatesIntegrationTests : IAsyncLifetime
{
    private readonly NotificationsPostgresFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task Handle_WhenNoTemplatesExist_ReturnsAnEmptyList()
    {
        await using var session = _fixture.Store.QuerySession();
        var response = await ViewNotificationTemplatesHandler.Handle(session, CancellationToken.None);

        response.Templates.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsEveryTemplateWithItsLockState()
    {
        var unlocked = NotificationTemplate.Create("application_approved", "Application Approved", "You're approved!", "Congrats!");
        var locked = NotificationTemplate.Create("application_rejected", "Application Rejected", "Update on your application", "...");
        locked.Edit(Guid.NewGuid(), "Updated subject", "Updated body");

        await using (var seedSession = _fixture.Store.LightweightSession())
        {
            seedSession.Store(unlocked, locked);
            await seedSession.SaveChangesAsync();
        }

        await using var session = _fixture.Store.QuerySession();
        var response = await ViewNotificationTemplatesHandler.Handle(session, CancellationToken.None);

        response.Templates.Should().HaveCount(2);
        response.Templates.Should().Contain(t => t.TemplateId == unlocked.Id && t.Name == "Application Approved" && !t.Locked);
        response.Templates.Should().Contain(t => t.TemplateId == locked.Id && t.Name == "Application Rejected" && t.Locked);
    }
}
