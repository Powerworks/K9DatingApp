using System.ComponentModel.DataAnnotations;

namespace PawMatch.Modules.Discovery.Api.Commands.SwipeOnDog;

/// <summary>
/// Validated by Wolverine.Http's built-in DataAnnotations middleware (see
/// Program.cs's MapWolverineEndpoints call). Guid-not-empty and the
/// cross-field "can't swipe on itself" rule can't be expressed as plain
/// attributes, so they live in IValidatableObject.Validate below - was
/// previously a separate SwipeOnDogValidator (FluentValidation), which
/// never actually ran against HTTP endpoints; see
/// RequestShelterAccountRequest for the fuller writeup of why.
/// </summary>
public sealed record SwipeOnDogRequest(Guid SwiperDogId, Guid TargetDogId, bool Liked) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SwiperDogId == Guid.Empty)
            yield return new ValidationResult("SwiperDogId is required.", [nameof(SwiperDogId)]);

        if (TargetDogId == Guid.Empty)
            yield return new ValidationResult("TargetDogId is required.", [nameof(TargetDogId)]);

        if (SwiperDogId != Guid.Empty && SwiperDogId == TargetDogId)
            yield return new ValidationResult("A dog cannot swipe on itself.", [nameof(TargetDogId)]);
    }
}

/// <summary>
/// Deliberately does NOT report whether a match resulted from this swipe.
/// That's a separate decision (see Automations/DetectMutualMatch) - this
/// slice's only job is "record the swipe." See the Event Modeling
/// blueprint doc for the UX implication: the client learns about a match
/// via a real-time push (SignalR) or by polling the match read model, not
/// synchronously from this response.
/// </summary>
public sealed record SwipeOnDogResponse(bool Acknowledged);
