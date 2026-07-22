using Marten;
using K9Crush.Modules.Notifications.Api.Infrastructure;
using K9Crush.Modules.Notifications.Domain;
using K9Crush.Modules.ShelterAdoption.Contracts;

namespace K9Crush.Modules.Notifications.Api.Automations.NotifyOnApplicationApproved;

/// <summary>
/// Automation slice: EVENT(ApplicationApprovedV1, cross-module from
/// ShelterAdoption) -> AUTOMATION -> email + NotificationLog. The emlang
/// yaml's "Send Approval Notification" -> "Approval Notification Sent",
/// previously deferred (see ApproveApplicationHandler's prior doc
/// comment) pending this module. Uses NotificationType.ApplicationStatus,
/// same category as NotifyOnApplicationRejectedHandler.
/// </summary>
public static class NotifyOnApplicationApprovedHandler
{
    public static Task Handle(
        ApplicationApprovedV1 integrationEvent,
        IDocumentSession session,
        ISmtpNotificationSender sender,
        CancellationToken cancellationToken)
    {
        var subject = $"Great news about your application for {integrationEvent.DogName}!";
        var body = $"Your application for {integrationEvent.DogName} was approved. Congratulations!";

        return NotificationDispatcher.DispatchAsync(
            session, sender, integrationEvent.ApplicantOwnerId, NotificationType.ApplicationStatus, subject, body, cancellationToken);
    }
}
