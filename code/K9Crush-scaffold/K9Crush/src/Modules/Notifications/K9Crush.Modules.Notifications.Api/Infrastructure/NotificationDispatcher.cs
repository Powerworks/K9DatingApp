using Marten;
using K9Crush.Modules.Notifications.Domain;

namespace K9Crush.Modules.Notifications.Api.Infrastructure;

/// <summary>
/// Shared by every notification-sending automation (NotifyOnMatchHandler,
/// NotifyOnApplicationRejectedHandler, NotifyOnApplicationApprovedHandler)
/// - the "check preference, check contact, send-or-suppress, log either
/// way" shape is identical across all of them, only the type/subject/body
/// differ per trigger event. Extracted once a third call site needed it
/// (see docs/04-high-level-design.md Section 1.5's "checks
/// NotificationPreference... before deciding email vs push vs suppress").
///
/// ADR-031: NotificationPreference/OwnerContact stay plain LoadAsync reads
/// (see NotificationPreference.cs's own doc comment for why this isn't the
/// ADR-019 MatchAggregate pattern - this dispatcher never mutates either
/// entity). NotificationLog becomes a fresh event stream per call
/// (StartStream with a new Guid) instead of a Store() upsert - it never
/// had an identity worth reusing, same as before.
/// </summary>
public static class NotificationDispatcher
{
    public static async Task DispatchAsync(
        IDocumentSession session,
        ISmtpNotificationSender sender,
        Guid ownerId,
        NotificationType type,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var preference = await session.LoadAsync<NotificationPreference>(ownerId, cancellationToken);
        var contact = await session.LoadAsync<OwnerContact>(ownerId, cancellationToken);

        var shouldSend = (preference?.IsEnabled(type) ?? true) && contact is not null;

        var (log, logEvent) = shouldSend
            ? NotificationLog.Record(ownerId, type, NotificationChannel.Email, subject)
            : NotificationLog.Record(ownerId, type, NotificationChannel.Suppressed, subject);

        if (shouldSend)
        {
            await sender.SendAsync(contact!.Email, subject, body, cancellationToken);
        }

        session.Events.StartStream<NotificationLog>(log.Id, logEvent);
        await session.SaveChangesAsync(cancellationToken);
    }
}
