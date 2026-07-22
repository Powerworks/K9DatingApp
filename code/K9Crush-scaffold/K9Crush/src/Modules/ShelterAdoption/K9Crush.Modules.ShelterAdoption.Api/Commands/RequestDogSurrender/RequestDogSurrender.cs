using System.ComponentModel.DataAnnotations;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.RequestDogSurrender;

/// <summary>The request/command for this slice - what the caller sends.</summary>
public sealed record RequestDogSurrenderRequest(
    [property: Required, MaxLength(50)] string DogName,
    [property: Required, MaxLength(50)] string Breed,
    [property: Range(0, 300)] int AgeInMonths,
    [property: Required, MaxLength(1000)] string ReasonForSurrender,
    [property: Required, MaxLength(1000)] string TemperamentNotes,
    [property: Required, MaxLength(1000)] string HealthNotes);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record RequestDogSurrenderResponse(Guid SurrenderRequestId);
