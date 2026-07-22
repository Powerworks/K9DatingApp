using System.ComponentModel.DataAnnotations;
using K9Crush.Modules.Media.Domain;

namespace K9Crush.Modules.Media.Api.Commands.ShareMedia;

/// <summary>
/// The request/command for this slice. SharedWithOwnerIds only means
/// anything when Visibility is SpecificPeople - enforced below since
/// that's a cross-field rule, same pattern as other IValidatableObject
/// requests in this codebase.
/// </summary>
public sealed record ShareMediaRequest(
    MediaVisibility Visibility,
    IReadOnlyList<Guid>? SharedWithOwnerIds) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Visibility == MediaVisibility.SpecificPeople && (SharedWithOwnerIds is null || SharedWithOwnerIds.Count == 0))
        {
            yield return new ValidationResult(
                "SharedWithOwnerIds is required when Visibility is SpecificPeople.", [nameof(SharedWithOwnerIds)]);
        }
    }
}

/// <summary>What this slice hands back to the caller.</summary>
public sealed record ShareMediaResponse(Guid MediaAssetId, string Visibility, DateTimeOffset SharedAt);
