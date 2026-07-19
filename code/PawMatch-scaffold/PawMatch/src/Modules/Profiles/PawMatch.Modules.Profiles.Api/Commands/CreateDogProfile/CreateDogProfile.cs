using System.ComponentModel.DataAnnotations;

namespace PawMatch.Modules.Profiles.Api.Commands.CreateDogProfile;

/// <summary>
/// The request/command for this slice - what the caller sends. Validated
/// by Wolverine.Http's built-in DataAnnotations middleware (see
/// Program.cs's MapWolverineEndpoints call) - was previously a separate
/// CreateDogProfileValidator (FluentValidation), which never actually ran
/// against HTTP endpoints; see RequestShelterAccountRequest for the fuller
/// writeup of why.
/// </summary>
public sealed record CreateDogProfileRequest(
    [property: Required, MaxLength(50)] string Name,
    [property: Required, MaxLength(50)] string Breed,
    [property: Range(0, 300)] int AgeInMonths,
    [property: MaxLength(500)] string Bio,
    [property: Range(-90, 90)] double Latitude,
    [property: Range(-180, 180)] double Longitude);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record CreateDogProfileResponse(Guid DogProfileId);
