using System.ComponentModel.DataAnnotations;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.PlaceDogInFoster;

/// <summary>
/// The request/command for this slice - what the caller sends.
/// FosterApplicationId, not the yaml's bare fosterCaregiverOwnerId - a
/// disclosed traceability fix (found via the same event-modeling
/// checklist pass that flagged DogListing.Status's missing Adopted guard):
/// the yaml never linked "Place Dog In Foster" back to *which* approved
/// application authorized the caregiver, only the caregiver's owner id.
/// The handler derives ApplicantOwnerId from the loaded, Approved
/// FosterApplication instead of trusting a bare caller-supplied id.
/// </summary>
public sealed record PlaceDogInFosterRequest(Guid FosterApplicationId) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FosterApplicationId == Guid.Empty)
            yield return new ValidationResult("FosterApplicationId is required.", [nameof(FosterApplicationId)]);
    }
}

/// <summary>What this slice hands back to the caller.</summary>
public sealed record PlaceDogInFosterResponse(Guid DogListingId, string Status, Guid FosterCaregiverOwnerId);
