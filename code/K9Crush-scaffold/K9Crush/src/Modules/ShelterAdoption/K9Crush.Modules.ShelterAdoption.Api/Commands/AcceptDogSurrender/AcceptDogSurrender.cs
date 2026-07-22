using System.ComponentModel.DataAnnotations;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.AcceptDogSurrender;

/// <summary>
/// The request/command for this slice - what the caller sends.
/// ShelterAccountId is a disclosed gap-fill: the yaml's "Accept Dog
/// Surrender" step never specifies which shelter receives the dog (its
/// only prop is `cascadedTo: ShelterManagingListings`), but
/// DogListing.Create requires one - Admin must pick the destination
/// shelter explicitly, same class of gap-fill as Places' CreatePlaceListing.
/// </summary>
public sealed record AcceptDogSurrenderRequest(Guid ShelterAccountId) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ShelterAccountId == Guid.Empty)
            yield return new ValidationResult("ShelterAccountId is required.", [nameof(ShelterAccountId)]);
    }
}

/// <summary>What this slice hands back to the caller.</summary>
public sealed record AcceptDogSurrenderResponse(Guid SurrenderRequestId, string Status, Guid DogListingId);
