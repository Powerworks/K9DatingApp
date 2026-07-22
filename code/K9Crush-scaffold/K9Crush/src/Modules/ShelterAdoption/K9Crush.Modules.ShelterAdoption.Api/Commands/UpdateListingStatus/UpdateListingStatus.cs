using K9Crush.Modules.ShelterAdoption.Domain;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.UpdateListingStatus;

/// <summary>The request/command for this slice - what the caller sends.
/// Every DogListingStatus value except Adopted is a legal target (see
/// UpdateListingStatusHandler's own comment for why Adopted is off-limits
/// here) - that one guard needs the current DogListing loaded first, so
/// it lives in the handler, not as attribute-level validation on this
/// record.</summary>
public sealed record UpdateListingStatusRequest(DogListingStatus Status);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record UpdateListingStatusResponse(Guid DogListingId, DogListingStatus Status);
