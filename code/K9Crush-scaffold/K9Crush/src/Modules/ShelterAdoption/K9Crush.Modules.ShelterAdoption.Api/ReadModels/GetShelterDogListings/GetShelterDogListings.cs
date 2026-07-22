using K9Crush.Modules.ShelterAdoption.Domain;

namespace K9Crush.Modules.ShelterAdoption.Api.ReadModels.GetShelterDogListings;

public sealed record DogListingSummary(Guid DogListingId, string Name, string Breed, int AgeInMonths, DogListingStatus Status);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record ShelterDogListingsResponse(IReadOnlyList<DogListingSummary> Items);
