using FluentAssertions;
using Marten;
using NSubstitute;
using K9Crush.Modules.Discovery.Contracts;
using K9Crush.Modules.Notifications.Api.Automations.NotifyOnMatch;
using K9Crush.Modules.Notifications.Api.Infrastructure;
using K9Crush.Modules.Notifications.Domain;
using Xunit;

namespace K9Crush.Modules.Notifications.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - NotifyOnMatchHandler only calls
/// LoadAsync/Store/SaveChangesAsync (plus the injected
/// ISmtpNotificationSender), so IDocumentSession mocks cleanly here.
/// </summary>
public class NotifyOnMatchHandlerTests
{
    private static MatchCreatedV1 BuildMatch(Guid ownerAId, Guid ownerBId) => new(
        EventId: Guid.NewGuid(),
        OccurredAt: DateTimeOffset.UtcNow,
        MatchId: Guid.NewGuid(),
        DogAId: Guid.NewGuid(),
        DogBId: Guid.NewGuid(),
        OwnerAId: ownerAId,
        OwnerBId: ownerBId);

    [Fact]
    public async Task Handle_WhenBothOwnersHaveNoPreferenceDocumentAndKnownEmails_EmailsBothAndLogsBoth()
    {
        var ownerAId = Guid.NewGuid();
        var ownerBId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<NotificationPreference>(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((NotificationPreference?)null);
        session.LoadAsync<OwnerContact>(ownerAId, Arg.Any<CancellationToken>()).Returns(new OwnerContact { Id = ownerAId, Email = "a@example.com" });
        session.LoadAsync<OwnerContact>(ownerBId, Arg.Any<CancellationToken>()).Returns(new OwnerContact { Id = ownerBId, Email = "b@example.com" });
        var sender = Substitute.For<ISmtpNotificationSender>();

        await NotifyOnMatchHandler.Handle(BuildMatch(ownerAId, ownerBId), session, sender, CancellationToken.None);

        await sender.Received(1).SendAsync("a@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await sender.Received(1).SendAsync("b@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        session.Received(2).Store(Arg.Is<NotificationLog[]>(arr => arr != null && arr.Length == 1 && arr[0].Channel == NotificationChannel.Email));
    }

    [Fact]
    public async Task Handle_WhenAnOwnerDisabledMatchNotifications_SuppressesOnlyThatOwner()
    {
        var ownerAId = Guid.NewGuid();
        var ownerBId = Guid.NewGuid();
        var preferenceA = NotificationPreference.CreateDefault(ownerAId);
        preferenceA.SetEnabled(NotificationType.Matches, false);

        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<NotificationPreference>(ownerAId, Arg.Any<CancellationToken>()).Returns(preferenceA);
        session.LoadAsync<NotificationPreference>(ownerBId, Arg.Any<CancellationToken>()).Returns((NotificationPreference?)null);
        session.LoadAsync<OwnerContact>(ownerAId, Arg.Any<CancellationToken>()).Returns(new OwnerContact { Id = ownerAId, Email = "a@example.com" });
        session.LoadAsync<OwnerContact>(ownerBId, Arg.Any<CancellationToken>()).Returns(new OwnerContact { Id = ownerBId, Email = "b@example.com" });
        var sender = Substitute.For<ISmtpNotificationSender>();

        await NotifyOnMatchHandler.Handle(BuildMatch(ownerAId, ownerBId), session, sender, CancellationToken.None);

        await sender.DidNotReceive().SendAsync("a@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await sender.Received(1).SendAsync("b@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        session.Received(1).Store(Arg.Is<NotificationLog[]>(arr =>
arr != null &&             arr.Length == 1 && arr[0].OwnerId == ownerAId && arr[0].Channel == NotificationChannel.Suppressed));
        session.Received(1).Store(Arg.Is<NotificationLog[]>(arr =>
arr != null &&             arr.Length == 1 && arr[0].OwnerId == ownerBId && arr[0].Channel == NotificationChannel.Email));
    }

    [Fact]
    public async Task Handle_WhenAnOwnersEmailIsUnknown_SuppressesWithoutAttemptingToSend()
    {
        var ownerAId = Guid.NewGuid();
        var ownerBId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<NotificationPreference>(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((NotificationPreference?)null);
        session.LoadAsync<OwnerContact>(ownerAId, Arg.Any<CancellationToken>()).Returns((OwnerContact?)null); // OwnerRegisteredV1 hasn't been consumed yet
        session.LoadAsync<OwnerContact>(ownerBId, Arg.Any<CancellationToken>()).Returns(new OwnerContact { Id = ownerBId, Email = "b@example.com" });
        var sender = Substitute.For<ISmtpNotificationSender>();

        await NotifyOnMatchHandler.Handle(BuildMatch(ownerAId, ownerBId), session, sender, CancellationToken.None);

        await sender.DidNotReceive().SendAsync("a@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await sender.Received(1).SendAsync("b@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        session.Received(1).Store(Arg.Is<NotificationLog[]>(arr =>
arr != null &&             arr.Length == 1 && arr[0].OwnerId == ownerAId && arr[0].Channel == NotificationChannel.Suppressed));
    }
}
