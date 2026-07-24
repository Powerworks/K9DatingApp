using K9Crush.Modules.Notifications.Domain;

namespace K9Crush.Modules.Notifications.Domain.Events;

public sealed record NotificationPreferenceCreatedV1(Guid OwnerId, IReadOnlyList<NotificationType> InitiallyEnabled);

public sealed record NotificationPreferenceUpdatedV1(NotificationType NotificationType, bool Enabled);
