using Marten;
using K9Crush.Modules.Notifications.Api.Infrastructure;
using K9Crush.Modules.Notifications.Domain;
using K9Crush.Modules.ShelterAdoption.Contracts;

namespace K9Crush.Modules.Notifications.Api.Automations.NotifyOnApplicationListingChanged;

/// <summary>
/// Automation slice: EVENT(ApplicationListingChangedV1, cross-module from
/// ShelterAdoption) -> AUTOMATION -> email + NotificationLog. The emlang
/// yaml's "Notify Applicant Of Listing Change" -> "Applicant Notified Of
/// Listing Change" (ADR-028's significant-edit chain).
/// </summary>
public static class NotifyOnApplicationListingChangedHandler
{
    public static Task Handle(
        ApplicationListingChangedV1 integrationEvent,
        IDocumentSession session,
        ISmtpNotificationSender sender,
        CancellationToken cancellationToken)
    {
        var subject = $"{integrationEvent.DogName}'s listing has been updated";
        var body = $"The shelter made a significant update to {integrationEvent.DogName}'s listing, which you have an open application for.";

        return NotificationDispatcher.DispatchAsync(
            session, sender, integrationEvent.ApplicantOwnerId, NotificationType.ApplicationStatus, subject, body, cancellationToken);
    }
}
