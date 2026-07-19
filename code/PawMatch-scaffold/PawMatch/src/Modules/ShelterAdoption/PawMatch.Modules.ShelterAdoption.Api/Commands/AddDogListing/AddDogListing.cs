using System.ComponentModel.DataAnnotations;

namespace PawMatch.Modules.ShelterAdoption.Api.Commands.AddDogListing;

/// <summary>
/// The request/command for this slice - what the caller sends. Named
/// AddDogListing rather than AddFirstDogListing (the emlang yaml's name
/// for this specific step) - see AddDogListingHandler's comment for why
/// this covers both "Add First Dog Listing" and the later chapter's "Add
/// Dog Listing" as one implementation.
/// </summary>
public sealed record AddDogListingRequest(
    [property: Required, MaxLength(50)] string Name,
    [property: Required, MaxLength(50)] string Breed,
    [property: Range(0, 300)] int AgeInMonths,
    [property: MaxLength(500)] string Bio);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record AddDogListingResponse(Guid DogListingId);
