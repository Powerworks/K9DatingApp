using System.Security.Claims;
using FluentAssertions;
using Marten;
using NSubstitute;
using K9Crush.Modules.Notifications.Api.Commands.UpdateNotificationPreferences;
using K9Crush.Modules.Notifications.Domain;
using Xunit;

namespace K9Crush.Modules.Notifications.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - UpdateNotificationPreferencesHandler
/// only calls LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here.
/// </summary>
public class UpdateNotificationPreferencesHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenNoPreferenceDocumentExistsYet_LazyCreatesADefaultThenAppliesTheUpdate()
    {
        var ownerId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<NotificationPreference>(ownerId, Arg.Any<CancellationToken>()).Returns((NotificationPreference?)null);

        var response = await UpdateNotificationPreferencesHandler.Handle(
            new UpdateNotificationPreferencesRequest(NotificationType.Messages, false),
            BuildUser(ownerId), session, CancellationToken.None);

        response.NotificationType.Should().Be(NotificationType.Messages);
        response.Enabled.Should().BeFalse();

        session.Received(1).Store(Arg.Is<NotificationPreference[]>(arr =>
            arr.Length == 1 &&
            arr[0].OwnerId == ownerId &&
            !arr[0].IsEnabled(NotificationType.Messages) &&
            arr[0].IsEnabled(NotificationType.ActivityFeed))); // other types remain enabled by default
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAPreferenceDocumentAlreadyExists_UpdatesItInPlace()
    {
        var ownerId = Guid.NewGuid();
        var preference = NotificationPreference.CreateDefault(ownerId);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<NotificationPreference>(ownerId, Arg.Any<CancellationToken>()).Returns(preference);

        await UpdateNotificationPreferencesHandler.Handle(
            new UpdateNotificationPreferencesRequest(NotificationType.ActivityFeed, false),
            BuildUser(ownerId), session, CancellationToken.None);

        preference.IsEnabled(NotificationType.ActivityFeed).Should().BeFalse();
        session.Received(1).Store(Arg.Is<NotificationPreference[]>(arr => arr.Length == 1 && arr[0] == preference));
    }
}
