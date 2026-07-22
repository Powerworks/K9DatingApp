using Marten;
using K9Crush.BuildingBlocks.Domain;
using K9Crush.Modules.Identity.Domain;
using K9Crush.Modules.ShelterAdoption.Contracts;

namespace K9Crush.Modules.Identity.Api.Automations.PromoteOwnerToShelterOnAccountCreated;

/// <summary>
/// Automation slice: EVENT(ShelterAccountCreatedV1, cross-module via
/// RabbitMQ) -> AUTOMATION -> mutates OwnerAccount.Role. This is the only
/// mechanism by which anyone reaches OwnerRole.Shelter today (ADR-017) -
/// see OwnerAccount.PromoteToShelter()'s comment.
///
/// Named *Handler per the class-naming requirement discovered while
/// fixing the RabbitMQ listener gap (Wolverine's convention-based
/// discovery silently never finds a plain Handle method otherwise - see
/// docs/05-event-modeling-blueprint.md Section 5.2).
///
/// Idempotent: promoting an owner who's already Shelter is a no-op, same
/// as every other automation reacting to an at-least-once delivered
/// event in this codebase.
/// </summary>
public static class PromoteOwnerToShelterOnAccountCreatedHandler
{
    public static async Task Handle(ShelterAccountCreatedV1 integrationEvent, IDocumentSession session, CancellationToken cancellationToken)
    {
        var ownerAccount = await session.LoadAsync<OwnerAccount>(integrationEvent.OwnerId, cancellationToken);
        if (ownerAccount is null || ownerAccount.Role == OwnerRole.Shelter)
            return;

        ownerAccount.PromoteToShelter();
        session.Store(ownerAccount);
        await session.SaveChangesAsync(cancellationToken);
    }
}
