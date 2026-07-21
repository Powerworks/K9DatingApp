namespace K9Crush.Modules.Notifications.Api.Commands.SaveNotificationTemplate;

/// <summary>
/// The request/command for this slice - what the caller sends.
/// AppliesToAlreadyQueuedNotifications matches the emlang yaml's own prop
/// name, but has no functional effect right now - nothing in this
/// codebase queues notifications for later delivery (NotifyOnMatchHandler
/// and friends all send synchronously, immediately), so there's nothing
/// for "retroactively apply to already-queued sends" to actually do.
/// Accepted and echoed back, not silently dropped, so the field isn't
/// invisible to a caller relying on the yaml's contract - not persisted
/// or acted on beyond that.
/// </summary>
public sealed record SaveNotificationTemplateRequest(bool AppliesToAlreadyQueuedNotifications);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record SaveNotificationTemplateResponse(Guid TemplateId, bool AppliesToAlreadyQueuedNotifications);
