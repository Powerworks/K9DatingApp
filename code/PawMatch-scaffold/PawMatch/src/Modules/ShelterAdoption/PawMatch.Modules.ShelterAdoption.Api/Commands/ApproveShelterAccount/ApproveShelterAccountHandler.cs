using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using PawMatch.Modules.ShelterAdoption.Contracts;
using PawMatch.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace PawMatch.Modules.ShelterAdoption.Api.Commands.ApproveShelterAccount;

/// <summary>
/// State-change slice: the emlang yaml's "Approve Shelter Account" - the
/// admin manual-override path to "Shelter Account Created," reachable
/// from VerificationIssuesFound directly (bypasses re-verification -
/// this is a deliberate reviewer judgment call to accept the account
/// despite flagged issues, distinct from CreateShelterAccountHandler's
/// normal path off Verified). Shares ShelterAccount.Activate() with that
/// handler - see its comment for why one domain method covers both.
///
/// Now gated by the Admin policy (see Program.cs) instead of
/// VerifiedOwner - closes the role-check gap this handler was originally
/// flagged with, arguably the most important one to close since this is
/// specifically the endpoint that overrides a flagged concern.
/// </summary>
public static class ApproveShelterAccountHandler
{
    [WolverinePost("/api/v1/shelter-adoption/shelter-accounts/{shelterAccountId:guid}/approve")]
    [Authorize(Policy = "Admin")]
    public static async Task<(Results<Ok<ApproveShelterAccountResponse>, NotFound, Conflict<string>>, ShelterAccountCreatedV1?)> Handle(
        Guid shelterAccountId,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var shelterAccount = await session.LoadAsync<ShelterAccount>(shelterAccountId, cancellationToken);
        if (shelterAccount is null)
            return (TypedResults.NotFound(), null);

        if (shelterAccount.Status != ShelterAccountStatus.VerificationIssuesFound)
            return (TypedResults.Conflict($"Cannot approve a shelter account in status {shelterAccount.Status}."), null);

        shelterAccount.Activate();
        session.Store(shelterAccount);
        await session.SaveChangesAsync(cancellationToken);

        var integrationEvent = new ShelterAccountCreatedV1(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            ShelterAccountId: shelterAccount.Id,
            OwnerId: shelterAccount.RequestedByOwnerId);

        return (TypedResults.Ok(new ApproveShelterAccountResponse(shelterAccount.Id, shelterAccount.Status.ToString())), integrationEvent);
    }
}
