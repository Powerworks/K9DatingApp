using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Notifications.Domain;

/// <summary>
/// Current-state Marten document - an audit record of a notification
/// decision, per HLD Section 1.5 ("Marten documents: NotificationPreference,
/// NotificationLog"). Written whether the notification was actually sent
/// or suppressed by preference, so the history is complete either way.
/// </summary>
public enum NotificationChannel
{
    Email,
    Suppressed
}

public class NotificationLog : Entity
{
    [JsonInclude] public Guid OwnerId { get; private set; }
    [JsonInclude] public NotificationType Type { get; private set; }
    [JsonInclude] public NotificationChannel Channel { get; private set; }
    [JsonInclude] public string Subject { get; private set; } = string.Empty;
    [JsonInclude] public DateTimeOffset OccurredAt { get; private set; }

    [JsonConstructor]
    private NotificationLog() { }

    public static NotificationLog Record(Guid ownerId, NotificationType type, NotificationChannel channel, string subject) => new()
    {
        OwnerId = ownerId,
        Type = type,
        Channel = channel,
        Subject = subject,
        OccurredAt = DateTimeOffset.UtcNow
    };
}
