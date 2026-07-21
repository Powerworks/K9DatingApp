using System.ComponentModel.DataAnnotations;

namespace K9Crush.Modules.Profiles.Api.Commands.AddDogProfilePhoto;

/// <summary>
/// The request/command for this slice - what the caller sends.
/// Guid-not-empty can't be expressed as a plain attribute, so it lives in
/// IValidatableObject.Validate below - same pattern as SwipeOnDogRequest.
/// </summary>
public sealed record AddDogProfilePhotoRequest(Guid MediaAssetId) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MediaAssetId == Guid.Empty)
            yield return new ValidationResult("MediaAssetId is required.", [nameof(MediaAssetId)]);
    }
}

/// <summary>What this slice hands back to the caller.</summary>
public sealed record AddDogProfilePhotoResponse(Guid DogProfileId, IReadOnlyList<Guid> PhotoIds);
