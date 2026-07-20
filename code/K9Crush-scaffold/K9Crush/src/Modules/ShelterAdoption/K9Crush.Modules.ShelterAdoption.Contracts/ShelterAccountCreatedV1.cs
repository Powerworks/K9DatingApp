using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.ShelterAdoption.Contracts;

/// <summary>
/// Published when a ShelterAccount activates (see
/// ShelterAccount.Activate() - both CreateShelterAccountHandler's normal
/// path and ApproveShelterAccountHandler's admin-override path cascade
/// this, since both call Activate()). Consumed by Identity
/// (Automations/PromoteOwnerToShelterOnAccountCreated) to promote the
/// requesting owner to the Shelter role (ADR-017) - this is the only
/// mechanism by which anyone reaches Shelter role today, since there's
/// no general-purpose AssignRole command.
/// </summary>
public sealed record ShelterAccountCreatedV1(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid ShelterAccountId,
    Guid OwnerId) : IIntegrationEvent;
