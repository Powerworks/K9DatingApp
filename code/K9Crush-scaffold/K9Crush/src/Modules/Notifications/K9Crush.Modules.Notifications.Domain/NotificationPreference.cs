using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;
using K9Crush.Modules.Notifications.Domain.Events;

namespace K9Crush.Modules.Notifications.Domain;

/// <summary>
/// One per owner. The emlang yaml's ManagingNotificationPreferences
/// chapter ("View/Update Notification Preferences"). Opt-out model: every
/// NotificationType starts enabled - a fresh CreateDefaultNew() document
/// has everything on, and UpdateNotificationPreferencesHandler turns
/// individual categories off.
///
/// The yaml's Update command/event also carry a "mandatory" prop
/// (alongside notificationType) with no scenario exercising a true case
/// and no clear caller-facing meaning (a member setting their own
/// preference as "mandatory" doesn't read as member-controlled data) -
/// not modeled as a stored/settable field here; see
/// UpdateNotificationPreferencesHandler's doc comment.
///
/// Self-aggregating event-sourced entity (ADR-031, Phase 3/5). Registered
/// as its own Inline snapshot in NotificationsModule.cs, since
/// ViewNotificationPreferencesHandler genuinely queries it by id.
/// NotificationDispatcher (used by every Notify* automation) also reads it
/// via plain LoadAsync against that same snapshot - not an ADR-019
/// violation, since Dispatcher never mutates NotificationPreference or
/// decides ITS validity, only reads a fact from it to inform an unrelated
/// write (NotificationLog) - the same "read a different entity purely for
/// an informational/auth check" shape ApproveApplicationHandler already
/// uses reading ShelterAccount.
/// </summary>
public class NotificationPreference : Entity
{
    [JsonInclude] public Guid OwnerId { get; private set; }
    [JsonInclude] public HashSet<NotificationType> Enabled { get; private set; } = new();

    [JsonConstructor]
    private NotificationPreference() { }

    public static NotificationPreference Create(NotificationPreferenceCreatedV1 e)
    {
        var preference = new NotificationPreference
        {
            OwnerId = e.OwnerId,
            Enabled = e.InitiallyEnabled.ToHashSet()
        };
        preference.Id = e.OwnerId;
        return preference;
    }

    /// <summary>
    /// Id is set to ownerId directly (not Entity's default random Guid) -
    /// one preference document per owner is a natural 1:1 key, so callers
    /// can FetchForWriting&lt;NotificationPreference&gt;(ownerId)/
    /// LoadAsync&lt;NotificationPreference&gt;(ownerId) directly instead of
    /// needing a query.
    /// </summary>
    public static (NotificationPreference Preference, NotificationPreferenceCreatedV1 Event) CreateDefaultNew(Guid ownerId)
    {
        var @event = new NotificationPreferenceCreatedV1(ownerId, Enum.GetValues<NotificationType>().ToList());
        return (Create(@event), @event);
    }

    public bool IsEnabled(NotificationType type) => Enabled.Contains(type);

    public void Apply(NotificationPreferenceUpdatedV1 e)
    {
        if (e.Enabled)
            Enabled.Add(e.NotificationType);
        else
            Enabled.Remove(e.NotificationType);
    }

    public NotificationPreferenceUpdatedV1 SetEnabled(NotificationType type, bool enabled)
    {
        var @event = new NotificationPreferenceUpdatedV1(type, enabled);
        Apply(@event);
        return @event;
    }
}
