namespace K9Crush.Modules.Admin.Domain.Events;

/// <summary>
/// ADR-031 event-sourcing retrofit, Phase 2/5. One record per
/// FeedbackInboxItem transition, matching the entity's own domain methods
/// 1:1 - see MediaAssetEvents.cs (Phase 1) for the naming/location
/// convention this follows.
/// </summary>
public sealed record FeedbackInboxItemCreatedV1(Guid FeedbackId, Guid OwnerId, string Message, DateTimeOffset SubmittedAt);

public sealed record FeedbackInboxItemRespondedV1(string ResponseMessage, DateTimeOffset RespondedAt);

public sealed record FeedbackInboxItemResolvedV1(DateTimeOffset ResolvedAt);
