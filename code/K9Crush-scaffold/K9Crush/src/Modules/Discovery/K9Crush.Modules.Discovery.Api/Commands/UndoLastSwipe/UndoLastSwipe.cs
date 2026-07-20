using System.ComponentModel.DataAnnotations;

namespace K9Crush.Modules.Discovery.Api.Commands.UndoLastSwipe;

/// <summary>
/// Validated by Wolverine.Http's DataAnnotations middleware (see
/// Program.cs's MapWolverineEndpoints call) - Guid-not-empty and the
/// "can't undo a swipe on itself" rule can't be expressed as plain
/// attributes, so they live in IValidatableObject, same pattern as
/// SwipeOnDogRequest.
/// </summary>
public sealed record UndoLastSwipeRequest(Guid SwiperDogId, Guid TargetDogId) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SwiperDogId == Guid.Empty)
            yield return new ValidationResult("SwiperDogId is required.", [nameof(SwiperDogId)]);

        if (TargetDogId == Guid.Empty)
            yield return new ValidationResult("TargetDogId is required.", [nameof(TargetDogId)]);

        if (SwiperDogId != Guid.Empty && SwiperDogId == TargetDogId)
            yield return new ValidationResult("A dog cannot undo a swipe on itself.", [nameof(TargetDogId)]);
    }
}

public sealed record UndoLastSwipeResponse(bool Acknowledged);
