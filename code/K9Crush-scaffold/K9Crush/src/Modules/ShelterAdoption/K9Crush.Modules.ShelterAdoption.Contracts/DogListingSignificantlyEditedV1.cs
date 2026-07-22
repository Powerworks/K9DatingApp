using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.ShelterAdoption.Contracts;

/// <summary>
/// Published when a shelter edits a dog listing AND flags the change as
/// significant (see EditDogListingHandler's significantChange request
/// field, matching the emlang yaml's own prop name) - the yaml's "Edit
/// Dog Listing" -> "Dog Listing Edited" event, but only the branch that
/// should notify applicants. Consumed by this SAME module
/// (Automations/NotifyApplicantsOfListingChange) - same same-module
/// cascade mechanism as DogListingRemovedV1, see ADR-028.
/// </summary>
public sealed record DogListingSignificantlyEditedV1(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid DogListingId,
    Guid ShelterAccountId,
    string DogName) : IIntegrationEvent;
