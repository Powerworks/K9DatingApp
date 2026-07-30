using K9Crush.Modules.ShelterAdoption.Domain;

namespace K9Crush.Modules.ShelterAdoption.Api.ReadModels.GetDogListingDetails;

/// <summary>What this slice hands back to the caller. Bio doubles as the
/// emlang yaml's "temperament" field - same underlying free-text
/// description, no separate field needed.</summary>
public sealed record DogListingDetailsResponse(
    Guid DogListingId,
    string Name,
    string Breed,
    int AgeInMonths,
    string Bio,
    Guid ShelterAccountId,
    DogListingStatus Status,
    IReadOnlyList<Guid> PhotoIds);
