using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Notifications.Domain;

/// <summary>
/// Current-state Marten document, one per owner. The emlang yaml's
/// ManagingNotificationPreferences chapter ("View/Update Notification
/// Preferences"). Opt-out model: every NotificationType starts enabled: a
/// fresh CreateDefault() document has everything on, and
/// UpdateNotificationPreferencesHandler turns individual categories off.
///
/// The yaml's Update command/event also carry a "mandatory" prop
/// (alongside notificationType) with no scenario exercising a true case
/// and no clear caller-facing meaning (a member setting their own
/// preference as "mandatory" doesn't read as member-controlled data) -
/// not modeled as a stored/settable field here; see
/// UpdateNotificationPreferencesHandler's doc comment.
/// </summary>
public class NotificationPreference : Entity
{
    [JsonInclude] public Guid OwnerId { get; private set; }
    [JsonInclude] public HashSet<NotificationType> Enabled { get; private set; } = new();

    [JsonConstructor]
    private NotificationPreference() { }

    /// <summary>
    /// Id is set to ownerId directly (not Entity's default random Guid) -
    /// one preference document per owner is a natural 1:1 key, same as
    /// DiscoveryFeedItem.Id being set to DogProfileId, so callers can
    /// LoadAsync&lt;NotificationPreference&gt;(ownerId) directly instead of
    /// needing a query.
    /// </summary>
    public static NotificationPreference CreateDefault(Guid ownerId)
    {
        var preference = new NotificationPreference
        {
            OwnerId = ownerId,
            Enabled = Enum.GetValues<NotificationType>().ToHashSet()
        };
        preference.Id = ownerId;
        return preference;
    }

    public bool IsEnabled(NotificationType type) => Enabled.Contains(type);

    public void SetEnabled(NotificationType type, bool enabled)
    {
        if (enabled)
            Enabled.Add(type);
        else
            Enabled.Remove(type);
    }
}
