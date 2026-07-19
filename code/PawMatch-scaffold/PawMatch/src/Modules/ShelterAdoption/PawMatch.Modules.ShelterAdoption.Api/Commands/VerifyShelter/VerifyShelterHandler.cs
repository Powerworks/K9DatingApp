using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using PawMatch.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace PawMatch.Modules.ShelterAdoption.Api.Commands.VerifyShelter;

/// <summary>
/// State-change slice: reviewer decision -> COMMAND -> mutates the
/// existing ShelterAccount document. Covers both "Verify Shelter" steps
/// in the emlang yaml (first-try verification and re-verification after
/// FlagVerificationIssues) - see ShelterAccount.Verify()'s comment for
/// why these collapse into one command.
///
/// Now gated by the Admin policy (ADR-017's role lookup, see Program.cs)
/// instead of VerifiedOwner - this reads as a platform-reviewer action,
/// even though the emlang yaml attributes it to the "Shelter Staff"
/// swimlane. Closes the role-check gap this handler was originally
/// flagged with.
/// </summary>
public static class VerifyShelterHandler
{
    [WolverinePost("/api/v1/shelter-adoption/shelter-accounts/{shelterAccountId:guid}/verify")]
    [Authorize(Policy = "Admin")]
    public static async Task<Results<Ok<VerifyShelterResponse>, NotFound, Conflict<string>>> Handle(
        Guid shelterAccountId,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var shelterAccount = await session.LoadAsync<ShelterAccount>(shelterAccountId, cancellationToken);
        if (shelterAccount is null)
            return TypedResults.NotFound();

        if (shelterAccount.Status != ShelterAccountStatus.Requested)
            return TypedResults.Conflict($"Cannot verify a shelter account in status {shelterAccount.Status}.");

        shelterAccount.Verify();
        session.Store(shelterAccount);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new VerifyShelterResponse(shelterAccount.Id, shelterAccount.Status.ToString()));
    }
}
