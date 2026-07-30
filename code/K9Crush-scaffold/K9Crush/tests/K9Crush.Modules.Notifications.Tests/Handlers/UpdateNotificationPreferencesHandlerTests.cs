using System.Security.Claims;
using FluentAssertions;
using Marten;
using NSubstitute;
using K9Crush.Modules.Notifications.Api.Commands.UpdateNotificationPreferences;
using K9Crush.Modules.Notifications.Domain;
using K9Crush.Modules.Notifications.Domain.Events;
using Xunit;

namespace K9Crush.Modules.Notifications.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - UpdateNotificationPreferencesHandler
/// only calls FetchForWriting/AppendOne/Events.StartStream/
/// SaveChangesAsync, so IDocumentSession mocks cleanly here (ADR-031).
/// </summary>
public class UpdateNotificationPreferencesHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenNoPreferenceStreamExistsYet_StartsANewStreamWithTheDefaultThenTheUpdate()
    {
        var ownerId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<NotificationPreference>(ownerId, null, out _);

        var response = await UpdateNotificationPreferencesHandler.Handle(
            new UpdateNotificationPreferencesRequest(NotificationType.Messages, false),
            BuildUser(ownerId), session, CancellationToken.None);

        response.NotificationType.Should().Be(NotificationType.Messages);
        response.Enabled.Should().BeFalse();

        session.Events.Received(1).StartStream<NotificationPreference>(
            ownerId,
            Arg.Is<object[]>(events => events != null && events.Length == 2
                && events[0] != null && ((NotificationPreferenceCreatedV1)events[0]).OwnerId == ownerId
                && events[1] != null && ((NotificationPreferenceUpdatedV1)events[1]).NotificationType == NotificationType.Messages
                && !((NotificationPreferenceUpdatedV1)events[1]).Enabled));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAPreferenceStreamAlreadyExists_AppendsTheUpdate()
    {
        var ownerId = Guid.NewGuid();
        var (preference, _) = NotificationPreference.CreateDefaultNew(ownerId);
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(ownerId, preference, out var stream);

        await UpdateNotificationPreferencesHandler.Handle(
            new UpdateNotificationPreferencesRequest(NotificationType.ActivityFeed, false),
            BuildUser(ownerId), session, CancellationToken.None);

        preference.IsEnabled(NotificationType.ActivityFeed).Should().BeFalse();
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && ((NotificationPreferenceUpdatedV1)o).NotificationType == NotificationType.ActivityFeed));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
