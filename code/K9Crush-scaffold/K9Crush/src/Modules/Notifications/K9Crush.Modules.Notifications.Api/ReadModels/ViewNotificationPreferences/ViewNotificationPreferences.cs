using K9Crush.Modules.Notifications.Domain;

namespace K9Crush.Modules.Notifications.Api.ReadModels.ViewNotificationPreferences;

/// <summary>
/// Mandatory is always false right now - see
/// UpdateNotificationPreferencesHandler's doc comment for why the yaml's
/// "mandatory" prop isn't modeled as caller-settable data.
/// </summary>
public sealed record NotificationPreferenceEntry(NotificationType NotificationType, bool Enabled, bool Mandatory);

public sealed record NotificationPreferencesResponse(IReadOnlyList<NotificationPreferenceEntry> Preferences);
