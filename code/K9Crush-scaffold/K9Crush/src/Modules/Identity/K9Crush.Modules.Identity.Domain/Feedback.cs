using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;
using K9Crush.Modules.Identity.Domain.Events;

namespace K9Crush.Modules.Identity.Domain;

/// <summary>
/// AccountProfileSettings' "Submit Feedback" -> "Feedback Submitted" -
/// deliberately parked here rather than in a dedicated Admin module at
/// the time this was first built (Admin exists now, but this entity's
/// own creation is what triggers Admin's cross-module copy - see
/// Admin.Domain.FeedbackInboxItem, Phase 2 - so it stays here). No read/
/// review slice on top of this Identity-side copy - only the write side
/// (Commands/SubmitFeedback) exists.
///
/// Self-aggregating event-sourced entity (ADR-031, Phase 4/5) - create-only,
/// no Apply overloads (nothing ever mutates a submitted feedback record),
/// same shape as Notifications' NotificationLog (Phase 3). No Inline
/// snapshot - nothing queries it from within this module.
/// </summary>
public class Feedback : Entity
{
    [JsonInclude] public Guid OwnerId { get; private set; }
    [JsonInclude] public string Message { get; private set; } = default!;
    [JsonInclude] public DateTimeOffset SubmittedAt { get; private set; }

    [JsonConstructor]
    private Feedback() { }

    public static Feedback Create(FeedbackRecordedV1 e) => new()
    {
        OwnerId = e.OwnerId,
        Message = e.Message,
        SubmittedAt = e.SubmittedAt
    };

    public static (Feedback Feedback, FeedbackRecordedV1 Event) Submit(Guid ownerId, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required.", nameof(message));

        var @event = new FeedbackRecordedV1(ownerId, message.Trim(), DateTimeOffset.UtcNow);
        return (Create(@event), @event);
    }
}
