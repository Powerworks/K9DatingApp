namespace K9Crush.Modules.Notifications.Domain.Events;

/// <summary>
/// ADR-031 event-sourcing retrofit, Phase 3/5. No handler in this codebase
/// ever appends this event today - the yaml has no "create a template"
/// step (see NotificationTemplate.cs's own doc comment on that gap) - but
/// it's kept for symmetry with every other entity's Create/Apply
/// convention, and as the entry point a future seed/create endpoint would use.
/// </summary>
public sealed record NotificationTemplateCreatedV1(string Key, string Name, string Subject, string Body);

/// <summary>
/// Lock-acquisition and content-change fused into one event, matching the
/// entity's own pre-retrofit Edit() method exactly - Locked/LockedByOwnerId
/// is genuine domain state surfaced in ViewNotificationTemplatesHandler's
/// read model (NotificationTemplateEntry.Locked), not just a write-time
/// concurrency mechanism, so it can't be replaced by Marten's own
/// FetchForExclusiveWriting session-level lock (that's invisible to a
/// second session's read, which is exactly what the read model needs).
/// </summary>
public sealed record NotificationTemplateEditedV1(Guid EditingOwnerId, string Subject, string Body);

public sealed record NotificationTemplateSavedV1;
