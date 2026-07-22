using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Identity.Domain;

/// <summary>
/// Current-state Marten document. AccountProfileSettings' "Submit
/// Feedback" -> "Feedback Submitted" - deliberately parked here rather
/// than in a dedicated Admin module, which doesn't exist yet (see
/// module_boundaries memory / docs/03-solution-architecture.md's proposed
/// module map). Same "no destination module yet, land it somewhere real
/// instead of nowhere" deferral as this codebase's other "not built yet"
/// gaps. No read/review slice on top of this yet - only the write side
/// (Commands/SubmitFeedback) exists this increment.
/// </summary>
public class Feedback : Entity
{
    [JsonInclude] public Guid OwnerId { get; private set; }
    [JsonInclude] public string Message { get; private set; } = default!;
    [JsonInclude] public DateTimeOffset SubmittedAt { get; private set; }

    [JsonConstructor]
    private Feedback() { }

    public static Feedback Submit(Guid ownerId, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required.", nameof(message));

        return new Feedback
        {
            OwnerId = ownerId,
            Message = message.Trim(),
            SubmittedAt = DateTimeOffset.UtcNow
        };
    }
}
