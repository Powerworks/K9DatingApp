using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Admin.Domain;

/// <summary>
/// Current-state Marten document. This module's own copy of a feedback
/// submission, built from Identity's cross-module FeedbackSubmittedV1
/// (see Api/ReadModels/Projectors/FeedbackSubmittedProjectorHandler) -
/// never a direct read of Identity's own Feedback document (module
/// isolation). Id is deliberately set to the originating FeedbackId, not
/// a fresh Guid, so redelivery of the same event is a safe upsert.
///
/// The emlang yaml's HandlingGeneralFeedbackSupport chapter's "Feedback
/// Inbox"/"Feedback Detail" state-views read this directly.
/// </summary>
public enum FeedbackStatus
{
    Open,
    Responded,
    Resolved
}

public class FeedbackInboxItem : Entity
{
    [JsonInclude] public Guid OwnerId { get; private set; }
    [JsonInclude] public string Message { get; private set; } = default!;
    [JsonInclude] public DateTimeOffset SubmittedAt { get; private set; }
    [JsonInclude] public FeedbackStatus Status { get; private set; }
    [JsonInclude] public string? ResponseMessage { get; private set; }
    [JsonInclude] public DateTimeOffset? RespondedAt { get; private set; }
    [JsonInclude] public DateTimeOffset? ResolvedAt { get; private set; }

    [JsonConstructor]
    private FeedbackInboxItem() { }

    public static FeedbackInboxItem Create(Guid feedbackId, Guid ownerId, string message, DateTimeOffset submittedAt)
    {
        return new FeedbackInboxItem
        {
            Id = feedbackId,
            OwnerId = ownerId,
            Message = message,
            SubmittedAt = submittedAt,
            Status = FeedbackStatus.Open
        };
    }

    /// <summary>The emlang yaml's "Respond To Feedback" -> "Feedback Responded". State-guard lives in the handler.</summary>
    public void Respond(string responseMessage)
    {
        ResponseMessage = responseMessage.Trim();
        RespondedAt = DateTimeOffset.UtcNow;
        Status = FeedbackStatus.Responded;
    }

    /// <summary>
    /// The emlang yaml's "Resolve Feedback" -> "Feedback Resolved" - only
    /// valid after a response (the yaml's own given/when/then: given
    /// "Feedback Responded", when "Resolve Feedback"). State-guard lives
    /// in the handler.
    /// </summary>
    public void Resolve()
    {
        ResolvedAt = DateTimeOffset.UtcNow;
        Status = FeedbackStatus.Resolved;
    }
}
