using Marten;
using NSubstitute;
using K9Crush.Modules.Notifications.Api.Automations.NotifyOnApplicationCancelled;
using K9Crush.Modules.Notifications.Api.Infrastructure;
using K9Crush.Modules.Notifications.Domain;
using K9Crush.Modules.Notifications.Domain.Events;
using K9Crush.Modules.ShelterAdoption.Contracts;
using Xunit;

namespace K9Crush.Modules.Notifications.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - NotifyOnApplicationCancelledHandler
/// only calls LoadAsync/Events.StartStream/SaveChangesAsync (via
/// NotificationDispatcher), so IDocumentSession mocks cleanly here
/// (ADR-031).
/// </summary>
public class NotifyOnApplicationCancelledHandlerTests
{
    private static ApplicationCancelledV1 BuildEvent(Guid applicantOwnerId) => new(
        EventId: Guid.NewGuid(),
        OccurredAt: DateTimeOffset.UtcNow,
        ApplicationId: Guid.NewGuid(),
        ApplicantOwnerId: applicantOwnerId,
        DogListingId: Guid.NewGuid(),
        DogName: "Biscuit");

    [Fact]
    public async Task Handle_WhenApplicantHasAKnownEmailAndDefaultPreferences_SendsAndLogsEmail()
    {
        var applicantOwnerId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        var eventStore = Substitute.For<Marten.Events.IEventStoreOperations>();
        session.Events.Returns(eventStore);
        session.LoadAsync<NotificationPreference>(applicantOwnerId, Arg.Any<CancellationToken>()).Returns((NotificationPreference?)null);
        session.LoadAsync<OwnerContact>(applicantOwnerId, Arg.Any<CancellationToken>()).Returns(new OwnerContact { Id = applicantOwnerId, Email = "applicant@example.com" });
        var sender = Substitute.For<ISmtpNotificationSender>();

        await NotifyOnApplicationCancelledHandler.Handle(BuildEvent(applicantOwnerId), session, sender, CancellationToken.None);

        await sender.Received(1).SendAsync(
            "applicant@example.com",
            Arg.Is<string>(s => s != null && s.Contains("Biscuit")),
            Arg.Is<string>(b => b != null && b.Contains("Biscuit")),
            Arg.Any<CancellationToken>());
        eventStore.Received(1).StartStream<NotificationLog>(
            Arg.Any<Guid>(),
            Arg.Is<object[]>(events => events != null && events.Length == 1 && events[0] != null
                && ((NotificationLogRecordedV1)events[0]).Type == NotificationType.ApplicationStatus
                && ((NotificationLogRecordedV1)events[0]).Channel == NotificationChannel.Email));
    }

    [Fact]
    public async Task Handle_WhenApplicantsEmailIsUnknown_Suppresses()
    {
        var applicantOwnerId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        var eventStore = Substitute.For<Marten.Events.IEventStoreOperations>();
        session.Events.Returns(eventStore);
        session.LoadAsync<NotificationPreference>(applicantOwnerId, Arg.Any<CancellationToken>()).Returns((NotificationPreference?)null);
        session.LoadAsync<OwnerContact>(applicantOwnerId, Arg.Any<CancellationToken>()).Returns((OwnerContact?)null);
        var sender = Substitute.For<ISmtpNotificationSender>();

        await NotifyOnApplicationCancelledHandler.Handle(BuildEvent(applicantOwnerId), session, sender, CancellationToken.None);

        await sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default);
        eventStore.Received(1).StartStream<NotificationLog>(
            Arg.Any<Guid>(),
            Arg.Is<object[]>(events => events != null && events.Length == 1 && events[0] != null
                && ((NotificationLogRecordedV1)events[0]).Channel == NotificationChannel.Suppressed));
    }
}
