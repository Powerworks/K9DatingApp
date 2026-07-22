using K9Crush.Modules.ShelterAdoption.Domain;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.UpdateListingStatus;

/// <summary>The request/command for this slice - what the caller sends.
/// Every DogListingStatus value is a legal target, no cross-field
/// validation needed (System.Text.Json's enum binding already rejects an
/// unrecognized string with a 400 before this record is even
/// constructed).</summary>
public sealed record UpdateListingStatusRequest(DogListingStatus Status);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record UpdateListingStatusResponse(Guid DogListingId, DogListingStatus Status);
