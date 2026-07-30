using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;
using K9Crush.Modules.Notifications.Domain.Events;

namespace K9Crush.Modules.Notifications.Domain;

/// <summary>
/// An audit record of a notification decision, per HLD Section 1.5
/// ("Marten documents: NotificationPreference, NotificationLog"). Written
/// whether the notification was actually sent or suppressed by preference,
/// so the history is complete either way.
///
/// Self-aggregating event-sourced entity (ADR-031, Phase 3/5) - create-only,
/// no Apply overloads at all, since nothing ever mutates a log entry after
/// it's written. No Inline snapshot registered - nothing under
/// ReadModels/** queries NotificationLog today (same as Media's MediaAsset,
/// Phase 1).
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

    public static NotificationLog Create(NotificationLogRecordedV1 e) => new()
    {
        OwnerId = e.OwnerId,
        Type = e.Type,
        Channel = e.Channel,
        Subject = e.Subject,
        OccurredAt = e.OccurredAt
    };

    public static (NotificationLog Log, NotificationLogRecordedV1 Event) Record(Guid ownerId, NotificationType type, NotificationChannel channel, string subject)
    {
        var @event = new NotificationLogRecordedV1(ownerId, type, channel, subject, DateTimeOffset.UtcNow);
        return (Create(@event), @event);
    }
}
