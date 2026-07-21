using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Identity.Contracts;

/// <summary>
/// Published by Commands/SubmitFeedback - the emlang yaml's
/// AccountProfileSettings chapter's "Submit Feedback" -> "Feedback
/// Submitted" (also HandlingGeneralFeedbackSupport's entry point).
/// Consumed cross-module by the Admin module (ReadModels/Projectors/
/// FeedbackSubmittedProjectorHandler) to populate its own Feedback Inbox
/// read model - Identity's Feedback document remains the write journal,
/// Admin never queries it directly (module isolation).
/// </summary>
public sealed record FeedbackSubmittedV1(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid FeedbackId,
    Guid OwnerId,
    string Message,
    DateTimeOffset SubmittedAt) : IIntegrationEvent;
