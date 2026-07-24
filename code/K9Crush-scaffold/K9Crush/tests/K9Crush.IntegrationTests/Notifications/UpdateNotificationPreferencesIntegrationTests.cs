using FluentAssertions;
using K9Crush.Modules.Notifications.Api.Commands.UpdateNotificationPreferences;
using K9Crush.Modules.Notifications.Domain;
using Xunit;

namespace K9Crush.IntegrationTests.Notifications;

/// <summary>
/// Layer 3 (TestingApproach.md) - ADR-031 spike: proves the lazy-create
/// branch (FetchForWriting comes back with a null Aggregate, so the
/// handler calls Events.StartStream instead of AppendOne) actually works
/// against real Postgres, not just NSubstitute mocks - the mocked Layer 2
/// tests can assert the *call shape* but can't prove Marten accepts
/// StartStream immediately after a FetchForWriting on the same id in the
/// same session, or that a second call against the now-existing stream
/// correctly takes the AppendOne branch instead.
/// </summary>
[Collection(NotificationsPostgresCollection.Name)]
public class UpdateNotificationPreferencesIntegrationTests
{
    private readonly NotificationsPostgresFixture _fixture;

    public UpdateNotificationPreferencesIntegrationTests(NotificationsPostgresFixture fixture) => _fixture = fixture;

    private static System.Security.Claims.ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenNoStreamExistsYet_StartsStreamWithDefaultThenUpdateAndPersists()
    {
        var ownerId = Guid.NewGuid();

        await using (var session = _fixture.Store.LightweightSession())
        {
            var response = await UpdateNotificationPreferencesHandler.Handle(
                new UpdateNotificationPreferencesRequest(NotificationType.Messages, false), BuildUser(ownerId), session, CancellationToken.None);

            response.Enabled.Should().BeFalse();
        }

        await using var verifySession = _fixture.Store.QuerySession();
        var preference = await verifySession.LoadAsync<NotificationPreference>(ownerId);
        preference.Should().NotBeNull();
        preference!.IsEnabled(NotificationType.Messages).Should().BeFalse();
        preference.IsEnabled(NotificationType.ActivityFeed).Should().BeTrue("other types remain enabled by default");
    }

    [Fact]
    public async Task Handle_WhenCalledTwice_SecondCallAppendsRatherThanStartingASecondStream()
    {
        var ownerId = Guid.NewGuid();

        await using (var firstSession = _fixture.Store.LightweightSession())
        {
            await UpdateNotificationPreferencesHandler.Handle(
                new UpdateNotificationPreferencesRequest(NotificationType.Messages, false), BuildUser(ownerId), firstSession, CancellationToken.None);
        }

        await using (var secondSession = _fixture.Store.LightweightSession())
        {
            await UpdateNotificationPreferencesHandler.Handle(
                new UpdateNotificationPreferencesRequest(NotificationType.ActivityFeed, false), BuildUser(ownerId), secondSession, CancellationToken.None);
        }

        await using var verifySession = _fixture.Store.QuerySession();
        var preference = await verifySession.LoadAsync<NotificationPreference>(ownerId);
        preference.Should().NotBeNull();
        preference!.IsEnabled(NotificationType.Messages).Should().BeFalse();
        preference.IsEnabled(NotificationType.ActivityFeed).Should().BeFalse();
        preference.IsEnabled(NotificationType.ApplicationStatus).Should().BeTrue("untouched types remain enabled by default");
    }
}
