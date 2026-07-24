using K9Crush.Modules.Notifications.Domain;

namespace K9Crush.Modules.Notifications.Domain.Events;

/// <summary>
/// NotificationLog is create-only - it never transitions after being
/// written, so this is its only event. Already "the closest thing to a
/// natural event stream" in this codebase before the retrofit (an audit
/// record, one per notification decision); ADR-031 just makes that literal
/// instead of a Marten document Store() upsert.
/// </summary>
public sealed record NotificationLogRecordedV1(Guid OwnerId, NotificationType Type, NotificationChannel Channel, string Subject, DateTimeOffset OccurredAt);
