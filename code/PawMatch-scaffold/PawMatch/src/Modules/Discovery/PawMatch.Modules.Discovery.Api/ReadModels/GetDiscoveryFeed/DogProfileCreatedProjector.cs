using Marten;
using PawMatch.Modules.Discovery.Domain;
using PawMatch.Modules.Profiles.Contracts;

namespace PawMatch.Modules.Discovery.Api.ReadModels.GetDiscoveryFeed;

/// <summary>
/// This is the "EVENT → READMODEL" half of the GetDiscoveryFeed state-view
/// slice (see Event Modeling blueprint, Section 4) - it doesn't answer a
/// query itself, it keeps the DiscoveryFeedItem read model current so
/// GetDiscoveryFeedHandler never has to look past a plain document query.
///
/// Triggered by the cross-module integration event DogProfileCreatedV1
/// over RabbitMQ (a different trigger mechanism than the DetectMutualMatch
/// automation, which reacts to a same-module domain event via Marten
/// forwarding - both are valid ways a slice's "EVENT" side can fire).
///
/// Delivery is at-least-once; Wolverine's inbox (via WolverineFx.Marten)
/// deduplicates by envelope id automatically, so Store() below is safe to
/// run again on redelivery without producing duplicate feed entries
/// (Marten Store() is an upsert keyed by Id).
///
/// Was missing SaveChangesAsync() entirely - Store() only stages the
/// change in-session, IDocumentSession does not auto-flush just because
/// a handler takes it as a parameter (every other handler in this
/// codebase calls SaveChangesAsync explicitly; this one never did).
/// Confirmed live: the incoming envelope showed status "Handled" with no
/// exception in wolverine_dead_letters, yet discovery.mt_doc_discoveryfeeditem
/// stayed empty - the handler ran and silently no-opped.
/// </summary>
public static class DogProfileCreatedProjectorHandler
{
    public static async Task Handle(DogProfileCreatedV1 integrationEvent, IDocumentSession session, CancellationToken cancellationToken)
    {
        session.Store(new DiscoveryFeedItem
        {
            Id = integrationEvent.DogProfileId,
            OwnerId = integrationEvent.OwnerId,
            Breed = integrationEvent.Breed,
            Latitude = integrationEvent.Latitude,
            Longitude = integrationEvent.Longitude,
            IndexedAt = integrationEvent.OccurredAt
        });

        await session.SaveChangesAsync(cancellationToken);
    }
}
