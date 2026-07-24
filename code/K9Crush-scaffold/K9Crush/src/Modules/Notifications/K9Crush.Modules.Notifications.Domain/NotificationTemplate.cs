using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Notifications.Domain;

/// <summary>
/// Current-state Marten document. The emlang yaml's
/// ShelterConfiguresNotificationTemplates chapter ("View/Edit/Save
/// Notification Template", with pessimistic locking - "Template Edit
/// Blocked" when someone else is already editing).
///
/// The yaml has no "create a template" step at all - only View/Edit/Save
/// exist, and its props (templateId/name/locked/lockedBy) never show an
/// actual editable content field either. Both are genuine gaps in the
/// spec, not oversights here: Subject/Body are added because a
/// "notification template" with no content wouldn't do anything (same
/// class of disclosed gap-fill applied elsewhere in this build-out). No
/// seed/create endpoint is added, though - nothing in the yaml specifies
/// how templates come to exist, so ViewNotificationTemplatesHandler just
/// returns whatever's actually been created, which may be nothing. This
/// chapter also does NOT wire these templates into the live send path
/// (the existing Notify* automations still use their own inline
/// subject/body) - that would be a separate, larger change touching every
/// existing Notify* automation, not specified by this chapter, and
/// deliberately deferred.
/// </summary>
public class NotificationTemplate : Entity
{
    [JsonInclude] public string Key { get; private set; } = string.Empty;
    [JsonInclude] public string Name { get; private set; } = string.Empty;
    [JsonInclude] public string Subject { get; private set; } = string.Empty;
    [JsonInclude] public string Body { get; private set; } = string.Empty;
    [JsonInclude] public bool Locked { get; private set; }
    [JsonInclude] public Guid? LockedByOwnerId { get; private set; }

    [JsonConstructor]
    private NotificationTemplate() { }

    public static NotificationTemplate Create(string key, string name, string subject, string body) => new()
    {
        Key = key.Trim(),
        Name = name.Trim(),
        Subject = subject.Trim(),
        Body = body.Trim(),
        Locked = false
    };

    /// <summary>
    /// The emlang yaml's "Edit Notification Template" -> "Notification
    /// Template Edited" (props: lockedBy) - submits new content and
    /// acquires the lock in one step (the yaml shows no separate "start
    /// editing, then submit" pair). State-guard (blocked if already
    /// locked by someone else) lives in the handler.
    /// </summary>
    public void Edit(Guid editingOwnerId, string subject, string body)
    {
        Subject = subject.Trim();
        Body = body.Trim();
        Locked = true;
        LockedByOwnerId = editingOwnerId;
    }

    /// <summary>
    /// The emlang yaml's "Save Notification Template" -> "Notification
    /// Template Saved" - releases the lock. Content itself was already
    /// applied by Edit() above; Save is the "I'm done" step. State-guard
    /// (only the current lock holder can save) lives in the handler.
    /// </summary>
    public void Save()
    {
        Locked = false;
        LockedByOwnerId = null;
    }
}
