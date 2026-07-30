namespace K9Crush.Modules.ShelterAdoption.Api.Commands.EndFosterPlacement;

/// <summary>The reason a foster placement is ending - the yaml's own
/// enumerated set. Informational only, same as EditDogListing's
/// SignificantChange flag - not stored on DogListing, nothing reads it
/// back later.</summary>
public enum FosterPlacementEndReason
{
    MovedToNewFoster,
    ReturnedToShelter,
    AdoptedByFosterCaregiver
}

/// <summary>The request/command for this slice - what the caller sends.</summary>
public sealed record EndFosterPlacementRequest(FosterPlacementEndReason Reason);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record EndFosterPlacementResponse(Guid DogListingId, string Status);
