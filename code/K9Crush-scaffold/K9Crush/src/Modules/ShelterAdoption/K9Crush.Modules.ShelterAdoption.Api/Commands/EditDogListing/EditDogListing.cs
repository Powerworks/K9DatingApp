using System.ComponentModel.DataAnnotations;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.EditDogListing;

/// <summary>
/// The request/command for this slice - what the caller sends.
/// SignificantChange matches the emlang yaml's own prop name exactly -
/// the shelter staff member submitting the edit decides whether it's
/// significant enough to notify applicants, not something this codebase
/// infers by diffing field values (that would be inventing a business
/// rule the yaml never specifies).
/// </summary>
public sealed record EditDogListingRequest(
    [property: Required, MaxLength(50)] string Name,
    [property: Required, MaxLength(50)] string Breed,
    [property: Range(0, 300)] int AgeInMonths,
    [property: MaxLength(500)] string Bio,
    bool SignificantChange);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record EditDogListingResponse(Guid DogListingId);
