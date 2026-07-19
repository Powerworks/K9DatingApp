namespace PawMatch.Modules.ShelterAdoption.Api.ReadModels.GetAdoptionListings;

/// <summary>
/// ShelterAccountId, not a display name - ShelterAccount.BusinessDetails
/// is a single free-text field ("Sunny Paws Rescue, EIN 12-3456789"),
/// not a clean shelter name, so fabricating a "shelterName" string out of
/// it would be worse than just returning the id. Add a dedicated display
/// name field to ShelterAccount if this becomes a real product need.
/// </summary>
public sealed record AdoptionListingSummary(Guid DogListingId, string Name, string Breed, Guid ShelterAccountId);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record AdoptionListingsResponse(IReadOnlyList<AdoptionListingSummary> Items);
