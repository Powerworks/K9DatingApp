namespace K9Crush.Modules.Identity.Domain.Events;

/// <summary>
/// Feedback is create-only, same shape as Notifications' NotificationLog
/// (Phase 3) - one event, no Apply overloads at all. Named
/// FeedbackRecordedV1, not FeedbackSubmittedV1, to avoid colliding with
/// this module's own Contracts.FeedbackSubmittedV1 integration event that
/// SubmitFeedbackHandler cascades for the same moment.
/// </summary>
public sealed record FeedbackRecordedV1(Guid OwnerId, string Message, DateTimeOffset SubmittedAt);
