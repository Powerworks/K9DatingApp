namespace K9Crush.Modules.ShelterAdoption.Api.Commands.MarkFosterDogReadyForAdoption;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record MarkFosterDogReadyForAdoptionResponse(Guid DogListingId, string Status);
