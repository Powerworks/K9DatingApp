using Marten;
using NSubstitute;
using K9Crush.Modules.Notifications.Api.Automations.NotifyOnApplicationRejected;
using K9Crush.Modules.Notifications.Api.Infrastructure;
using K9Crush.Modules.Notifications.Domain;
using K9Crush.Modules.ShelterAdoption.Contracts;
using Xunit;

namespace K9Crush.Modules.Notifications.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - NotifyOnApplicationRejectedHandler only
/// calls LoadAsync/Store/SaveChangesAsync (via NotificationDispatcher),
/// so IDocumentSession mocks cleanly here.
/// </summary>
public class NotifyOnApplicationRejectedHandlerTests
{
    private static ApplicationRejectedV1 BuildEvent(Guid applicantOwnerId) => new(
        EventId: Guid.NewGuid(),
        OccurredAt: DateTimeOffset.UtcNow,
        ApplicationId: Guid.NewGuid(),
        ApplicantOwnerId: applicantOwnerId,
        DogListingId: Guid.NewGuid(),
        DogName: "Biscuit",
        RejectionReason: "Not enough yard space");

    [Fact]
    public async Task Handle_WhenApplicantHasAKnownEmailAndDefaultPreferences_SendsAndLogsEmail()
    {
        var applicantOwnerId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<NotificationPreference>(applicantOwnerId, Arg.Any<CancellationToken>()).Returns((NotificationPreference?)null);
        session.LoadAsync<OwnerContact>(applicantOwnerId, Arg.Any<CancellationToken>()).Returns(new OwnerContact { Id = applicantOwnerId, Email = "applicant@example.com" });
        var sender = Substitute.For<ISmtpNotificationSender>();

        await NotifyOnApplicationRejectedHandler.Handle(BuildEvent(applicantOwnerId), session, sender, CancellationToken.None);

        await sender.Received(1).SendAsync(
            "applicant@example.com",
            Arg.Is<string>(s => s != null && s.Contains("Biscuit")),
            Arg.Is<string>(b => b != null && b.Contains("Not enough yard space")),
            Arg.Any<CancellationToken>());
        session.Received(1).Store(Arg.Is<NotificationLog[]>(arr =>
arr != null &&             arr.Length == 1 && arr[0].Type == NotificationType.ApplicationStatus && arr[0].Channel == NotificationChannel.Email));
    }

    [Fact]
    public async Task Handle_WhenApplicantDisabledApplicationStatusNotifications_Suppresses()
    {
        var applicantOwnerId = Guid.NewGuid();
        var preference = NotificationPreference.CreateDefault(applicantOwnerId);
        preference.SetEnabled(NotificationType.ApplicationStatus, false);

        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<NotificationPreference>(applicantOwnerId, Arg.Any<CancellationToken>()).Returns(preference);
        session.LoadAsync<OwnerContact>(applicantOwnerId, Arg.Any<CancellationToken>()).Returns(new OwnerContact { Id = applicantOwnerId, Email = "applicant@example.com" });
        var sender = Substitute.For<ISmtpNotificationSender>();

        await NotifyOnApplicationRejectedHandler.Handle(BuildEvent(applicantOwnerId), session, sender, CancellationToken.None);

        await sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default);
        session.Received(1).Store(Arg.Is<NotificationLog[]>(arr => arr != null && arr.Length == 1 && arr[0].Channel == NotificationChannel.Suppressed));
    }
}
