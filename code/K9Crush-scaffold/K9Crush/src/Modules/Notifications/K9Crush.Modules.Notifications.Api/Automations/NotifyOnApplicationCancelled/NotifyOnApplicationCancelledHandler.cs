using Marten;
using K9Crush.Modules.Notifications.Api.Infrastructure;
using K9Crush.Modules.Notifications.Domain;
using K9Crush.Modules.ShelterAdoption.Contracts;

namespace K9Crush.Modules.Notifications.Api.Automations.NotifyOnApplicationCancelled;

/// <summary>
/// Automation slice: EVENT(ApplicationCancelledV1, cross-module from
/// ShelterAdoption) -> AUTOMATION -> email + NotificationLog. The emlang
/// yaml's "Notify Applicants Of Cancellation" -> "Applicants Notified Of
/// Cancellation" (ADR-028's final leg of the removed-listing chain).
/// </summary>
public static class NotifyOnApplicationCancelledHandler
{
    public static Task Handle(
        ApplicationCancelledV1 integrationEvent,
        IDocumentSession session,
        ISmtpNotificationSender sender,
        CancellationToken cancellationToken)
    {
        var subject = $"Your application for {integrationEvent.DogName} has been cancelled";
        var body = $"{integrationEvent.DogName}'s listing was removed by the shelter, so your open application for {integrationEvent.DogName} has been cancelled.";

        return NotificationDispatcher.DispatchAsync(
            session, sender, integrationEvent.ApplicantOwnerId, NotificationType.ApplicationStatus, subject, body, cancellationToken);
    }
}
