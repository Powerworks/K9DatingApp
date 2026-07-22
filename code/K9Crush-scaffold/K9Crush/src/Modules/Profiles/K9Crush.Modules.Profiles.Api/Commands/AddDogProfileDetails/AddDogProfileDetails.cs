using System.ComponentModel.DataAnnotations;

namespace K9Crush.Modules.Profiles.Api.Commands.AddDogProfileDetails;

/// <summary>
/// The request/command for this slice - what the caller sends. Latitude/
/// Longitude aren't one of the emlang yaml's own named props for this
/// step (only name/breed/age are) - see DogProfile.AddDetails()'s doc
/// comment for why they're carried here anyway.
/// </summary>
public sealed record AddDogProfileDetailsRequest(
    [property: Required, MaxLength(50)] string Name,
    [property: Required, MaxLength(50)] string Breed,
    [property: Range(0, 300)] int AgeInMonths,
    [property: MaxLength(500)] string Bio,
    [property: Range(-90, 90)] double Latitude,
    [property: Range(-180, 180)] double Longitude);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record AddDogProfileDetailsResponse(Guid DogProfileId);
