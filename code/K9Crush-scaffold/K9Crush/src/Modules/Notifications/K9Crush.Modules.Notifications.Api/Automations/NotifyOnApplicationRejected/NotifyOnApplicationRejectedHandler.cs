using Marten;
using K9Crush.Modules.Notifications.Api.Infrastructure;
using K9Crush.Modules.Notifications.Domain;
using K9Crush.Modules.ShelterAdoption.Contracts;

namespace K9Crush.Modules.Notifications.Api.Automations.NotifyOnApplicationRejected;

/// <summary>
/// Automation slice: EVENT(ApplicationRejectedV1, cross-module from
/// ShelterAdoption) -> AUTOMATION -> email + NotificationLog. The emlang
/// yaml's "Send Rejection Reason" -> "Rejection Reason Sent", previously
/// deferred (see RejectApplicationHandler's prior doc comment) pending
/// this module. Uses NotificationType.ApplicationStatus, one of the four
/// categories the yaml's ManagingNotificationPreferences chapter actually
/// names.
/// </summary>
public static class NotifyOnApplicationRejectedHandler
{
    public static Task Handle(
        ApplicationRejectedV1 integrationEvent,
        IDocumentSession session,
        ISmtpNotificationSender sender,
        CancellationToken cancellationToken)
    {
        var subject = $"Update on your application for {integrationEvent.DogName}";
        var body = $"Your application for {integrationEvent.DogName} was not approved. Reason: {integrationEvent.RejectionReason}";

        return NotificationDispatcher.DispatchAsync(
            session, sender, integrationEvent.ApplicantOwnerId, NotificationType.ApplicationStatus, subject, body, cancellationToken);
    }
}
