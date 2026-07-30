namespace K9Crush.Modules.ShelterAdoption.Domain.Events;

/// <summary>
/// ADR-031 event-sourcing retrofit, Phase 5/5 - one record per DogListing
/// transition. Named distinctly from
/// K9Crush.Modules.ShelterAdoption.Contracts.DogListingRemovedV1 (that's
/// the cross-module integration event cascaded on removal) -
/// <see cref="DogListingWithdrawnV1"/> is this module's own in-stream
/// no-hard-delete flag event; same "distinct from Contracts" split as
/// every other retrofitted entity in this phase.
/// </summary>
public sealed record DogListingAddedV1(
    Guid ShelterAccountId, string Name, string Breed, int AgeInMonths, string Bio, DateTimeOffset AddedAt);

public sealed record DogListingStatusUpdatedV1(DogListingStatus Status);

public sealed record DogListingPlacedInFosterV1(Guid FosterCaregiverOwnerId);

public sealed record FosterDogMarkedReadyForAdoptionV1;

public sealed record FosterPlacementEndedV1;

public sealed record DogListingEditedV1(string Name, string Breed, int AgeInMonths, string Bio);

public sealed record DogListingPhotoAddedV1(Guid MediaAssetId);

public sealed record DogListingWithdrawnV1;
