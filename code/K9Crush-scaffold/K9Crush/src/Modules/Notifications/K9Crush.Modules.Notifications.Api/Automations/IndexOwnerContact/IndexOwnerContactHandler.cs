using Marten;
using K9Crush.Modules.Identity.Contracts;
using K9Crush.Modules.Notifications.Domain;

namespace K9Crush.Modules.Notifications.Api.Automations.IndexOwnerContact;

/// <summary>
/// The "EVENT -> READMODEL" half of a state-view slice, same pattern as
/// Discovery's DogProfileCreatedProjectorHandler - keeps OwnerContact
/// current so NotifyOnMatchHandler never needs to reach into Identity's
/// own Domain (which it can't - Contracts-only cross-module boundary).
///
/// Delivery is at-least-once; Store() is an upsert keyed by Id, so
/// redelivery is safe without extra guarding.
/// </summary>
public static class IndexOwnerContactHandler
{
    public static async Task Handle(OwnerRegisteredV1 integrationEvent, IDocumentSession session, CancellationToken cancellationToken)
    {
        session.Store(new OwnerContact
        {
            Id = integrationEvent.OwnerId,
            Email = integrationEvent.Email
        });

        await session.SaveChangesAsync(cancellationToken);
    }
}
