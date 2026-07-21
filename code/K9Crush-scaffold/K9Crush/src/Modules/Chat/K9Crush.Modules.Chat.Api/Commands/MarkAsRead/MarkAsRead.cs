using System.ComponentModel.DataAnnotations;

namespace K9Crush.Modules.Chat.Api.Commands.MarkAsRead;

/// <summary>
/// The request/command for this slice - what the caller sends.
/// Guid-not-empty can't be expressed as a plain attribute, so it lives in
/// IValidatableObject.Validate below - same pattern as SwipeOnDogRequest.
/// LastReadMessageId isn't validated against actually existing in this
/// conversation (would need aggregating every MessageSent id into state,
/// not needed for anything this increment does with it) - trusted from
/// the caller, same "don't invent a check nothing requires" restraint as
/// elsewhere in this codebase.
/// </summary>
public sealed record MarkAsReadRequest(Guid LastReadMessageId) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (LastReadMessageId == Guid.Empty)
            yield return new ValidationResult("LastReadMessageId is required.", [nameof(LastReadMessageId)]);
    }
}

/// <summary>What this slice hands back to the caller.</summary>
public sealed record MarkAsReadResponse(Guid ConversationId, Guid LastReadMessageId);
