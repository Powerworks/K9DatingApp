using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Identity.Contracts;

/// <summary>
/// Published by Commands/RequestAccountDeletion - the emlang yaml's
/// AccountProfileSettings chapter's "Request Account Deletion" ->
/// "Account Deletion Requested". Consumed cross-module by ShelterAdoption
/// (Automations/WithdrawApplicationsOnAccountDeletionRequested) to
/// silently withdraw the owner's open applications - the yaml's
/// "Withdraw Applications Before Deletion" step. Only carries OwnerId:
/// the yaml's "Open Items Flagged" step (with openApplicationsCount/
/// openPlaydatesCount props) and the shelter-side "Flag Active Listings"
/// branch are both deliberately deferred this increment - the former
/// needs either a forbidden cross-module query or an extra round-trip
/// event nothing yet acts on, the latter needs a ShelterAccount link the
/// yaml doesn't specify subsequent handling for.
/// </summary>
public sealed record AccountDeletionRequestedV1(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid OwnerId) : IIntegrationEvent;
