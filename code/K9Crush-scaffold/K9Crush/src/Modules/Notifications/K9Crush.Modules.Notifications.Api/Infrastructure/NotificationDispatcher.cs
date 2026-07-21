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

        if (shouldSend)
        {
            await sender.SendAsync(contact!.Email, subject, body, cancellationToken);
            session.Store(NotificationLog.Record(ownerId, type, NotificationChannel.Email, subject));
        }
        else
        {
            session.Store(NotificationLog.Record(ownerId, type, NotificationChannel.Suppressed, subject));
        }

        await session.SaveChangesAsync(cancellationToken);
    }
}
