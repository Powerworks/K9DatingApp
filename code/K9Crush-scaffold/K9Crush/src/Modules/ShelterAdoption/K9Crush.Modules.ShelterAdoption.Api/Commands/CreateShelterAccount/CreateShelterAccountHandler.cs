using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Contracts;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.CreateShelterAccount;

/// <summary>
/// State-change slice: the emlang yaml's "Create Shelter Account" ->
/// "Shelter Account Created" - only valid from Verified (reached via
/// VerifyShelterHandler). Route is /activate, not /create, since the
/// ShelterAccount document already exists at this point - see
/// ShelterAccount.Activate()'s comment.
///
/// Deliberately does NOT cover the emlang yaml's other path to the same
/// "Shelter Account Created" event - "Approve Shelter Account", a manual
/// admin override reachable from VerificationIssuesFound rather than
/// Verified. That's ApproveShelterAccountHandler, a separate command with
/// a different precondition, sharing this same cascaded event.
///
/// Now gated by the Admin policy (see Program.cs) instead of
/// VerifiedOwner - closes the role-check gap this handler was originally
/// flagged with.
/// </summary>
public static class CreateShelterAccountHandler
{
    [WolverinePost("/api/v1/shelter-adoption/shelter-accounts/{shelterAccountId:guid}/activate")]
    [Authorize(Policy = "Admin")]
    public static async Task<(Results<Ok<CreateShelterAccountResponse>, NotFound, Conflict<string>>, ShelterAccountCreatedV1?)> Handle(
        Guid shelterAccountId,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchForWriting<ShelterAccount>(shelterAccountId, cancellationToken);
        var shelterAccount = stream.Aggregate;
        if (shelterAccount is null)
            return (TypedResults.NotFound(), null);

        if (shelterAccount.Status != ShelterAccountStatus.Verified)
            return (TypedResults.Conflict($"Cannot activate a shelter account in status {shelterAccount.Status}."), null);

        var @event = shelterAccount.Activate();
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);

        var integrationEvent = new ShelterAccountCreatedV1(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            ShelterAccountId: shelterAccount.Id,
            OwnerId: shelterAccount.RequestedByOwnerId);

        return (TypedResults.Ok(new CreateShelterAccountResponse(shelterAccount.Id, shelterAccount.Status.ToString())), integrationEvent);
    }
}
