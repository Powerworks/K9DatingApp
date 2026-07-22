using Marten;
using K9Crush.Modules.Discovery.Contracts;
using K9Crush.Modules.Notifications.Api.Infrastructure;
using K9Crush.Modules.Notifications.Domain;

namespace K9Crush.Modules.Notifications.Api.Automations.NotifyOnMatch;

/// <summary>
/// Automation slice: EVENT(MatchCreatedV1, cross-module from Discovery)
/// -> AUTOMATION -> email + NotificationLog, for each of the two owners
/// independently. Per docs/04-high-level-design.md Section 1.5 ("Consumer
/// checks NotificationPreference... before deciding email vs push vs
/// suppress") - this slice implements the email/suppress half; push and
/// presence-based suppression aren't built yet (no push provider chosen,
/// no Redis presence wiring for this module).
///
/// Cross-module integration event trigger - needs
/// NotificationsModule.IntegrationEventQueueName bound to the shared
/// exchange, same mechanism as Discovery's own DogProfileCreatedProjector.
///
/// Always writes a NotificationLog entry (Email or Suppressed) even when
/// nothing is actually sent, so the history is complete either way - see
/// NotificationLog.cs.
/// </summary>
public static class NotifyOnMatchHandler
{
    private const string Subject = "You've got a new match on K9Crush!";
    private const string Body = "One of your dogs just matched with another dog!";

    public static async Task Handle(
        MatchCreatedV1 integrationEvent,
        IDocumentSession session,
        ISmtpNotificationSender sender,
        CancellationToken cancellationToken)
    {
        await NotificationDispatcher.DispatchAsync(
            session, sender, integrationEvent.OwnerAId, NotificationType.Matches, Subject, Body, cancellationToken);
        await NotificationDispatcher.DispatchAsync(
            session, sender, integrationEvent.OwnerBId, NotificationType.Matches, Subject, Body, cancellationToken);
    }
}
