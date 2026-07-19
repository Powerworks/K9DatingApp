using System.ComponentModel.DataAnnotations;

namespace PawMatch.Modules.ShelterAdoption.Api.Commands.EditDogListing;

/// <summary>The request/command for this slice - what the caller sends.</summary>
public sealed record EditDogListingRequest(
    [property: Required, MaxLength(50)] string Name,
    [property: Required, MaxLength(50)] string Breed,
    [property: Range(0, 300)] int AgeInMonths,
    [property: MaxLength(500)] string Bio);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record EditDogListingResponse(Guid DogListingId);
