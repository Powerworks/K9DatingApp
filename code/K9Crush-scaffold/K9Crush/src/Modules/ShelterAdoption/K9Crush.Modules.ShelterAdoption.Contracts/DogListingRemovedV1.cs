using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.ShelterAdoption.Contracts;

/// <summary>
/// Published when a shelter removes a dog listing (see
/// RemoveDogListingHandler) - the emlang yaml's ShelterManagingListings
/// chapter's "Remove Dog Listing" -> "Dog Listing Removed". Consumed by
/// this SAME module (Automations/CancelApplicationsForRemovedListing) via
/// ShelterAdoptionModule's own IntegrationEventQueueName - see ADR-028 for
/// why a same-module command cascade goes through the shared exchange
/// same as any cross-module event, rather than a separate "local-only"
/// mechanism.
///
/// DogName is captured here from the listing before it's deleted (the
/// delete is a genuine hard-delete - RemoveDogListingHandler/DogListing.cs)
/// so nothing downstream needs to have already known it.
/// </summary>
public sealed record DogListingRemovedV1(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid DogListingId,
    Guid ShelterAccountId,
    string DogName) : IIntegrationEvent;
