using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;
using K9Crush.Modules.Admin.Domain.Events;

namespace K9Crush.Modules.Admin.Domain;

/// <summary>
/// This module's own copy of a feedback submission, built from Identity's
/// cross-module FeedbackSubmittedV1 (see
/// Api/ReadModels/Projectors/FeedbackSubmittedProjectorHandler) - never a
/// direct read of Identity's own Feedback document (module isolation). Id
/// is deliberately set to the originating FeedbackId, not a fresh Guid -
/// under ADR-031 this makes it the first entity in the retrofit whose
/// stream id is externally supplied rather than freshly generated here
/// (Identity's Phase 4 will do the same with a Supabase user id).
///
/// The emlang yaml's HandlingGeneralFeedbackSupport chapter's "Feedback
/// Inbox"/"Feedback Detail" state-views read this directly - it's
/// registered as its own Inline snapshot in AdminModule.cs (ADR-031's
/// dual-use pattern: the same class serves FetchForWriting on the write
/// side and Query&lt;T&gt;/LoadAsync on the read side), since those two
/// ReadModels/** handlers genuinely need to query it.
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

    public static FeedbackInboxItem Create(FeedbackInboxItemCreatedV1 e) => new()
    {
        Id = e.FeedbackId,
        OwnerId = e.OwnerId,
        Message = e.Message,
        SubmittedAt = e.SubmittedAt,
        Status = FeedbackStatus.Open
    };

    public static (FeedbackInboxItem Item, FeedbackInboxItemCreatedV1 Event) CreateNew(Guid feedbackId, Guid ownerId, string message, DateTimeOffset submittedAt)
    {
        var @event = new FeedbackInboxItemCreatedV1(feedbackId, ownerId, message, submittedAt);
        return (Create(@event), @event);
    }

    public void Apply(FeedbackInboxItemRespondedV1 e)
    {
        ResponseMessage = e.ResponseMessage;
        RespondedAt = e.RespondedAt;
        Status = FeedbackStatus.Responded;
    }

    public void Apply(FeedbackInboxItemResolvedV1 e)
    {
        ResolvedAt = e.ResolvedAt;
        Status = FeedbackStatus.Resolved;
    }

    /// <summary>The emlang yaml's "Respond To Feedback" -> "Feedback Responded". State-guard lives in the handler.</summary>
    public FeedbackInboxItemRespondedV1 Respond(string responseMessage)
    {
        var @event = new FeedbackInboxItemRespondedV1(responseMessage.Trim(), DateTimeOffset.UtcNow);
        Apply(@event);
        return @event;
    }

    /// <summary>
    /// The emlang yaml's "Resolve Feedback" -> "Feedback Resolved" - only
    /// valid after a response (the yaml's own given/when/then: given
    /// "Feedback Responded", when "Resolve Feedback"). State-guard lives
    /// in the handler.
    /// </summary>
    public FeedbackInboxItemResolvedV1 Resolve()
    {
        var @event = new FeedbackInboxItemResolvedV1(DateTimeOffset.UtcNow);
        Apply(@event);
        return @event;
    }
}
