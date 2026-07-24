using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.FlagVerificationIssues;

/// <summary>
/// State-change slice: the emlang yaml's "Flag Verification Issues" ->
/// "Verification Issues Found" - the branch off Requested that
/// ResubmitShelterAccountRequest (not yet built) will later resolve back
/// to Requested for re-verification. Only valid from Requested.
///
/// Now gated by the Admin policy - see VerifyShelterHandler's comment,
/// not repeated here.
/// </summary>
public static class FlagVerificationIssuesHandler
{
    [WolverinePost("/api/v1/shelter-adoption/shelter-accounts/{shelterAccountId:guid}/flag-verification-issues")]
    [Authorize(Policy = "Admin")]
    public static async Task<Results<Ok<FlagVerificationIssuesResponse>, NotFound, Conflict<string>>> Handle(
        Guid shelterAccountId,
        FlagVerificationIssuesRequest request,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchForWriting<ShelterAccount>(shelterAccountId, cancellationToken);
        var shelterAccount = stream.Aggregate;
        if (shelterAccount is null)
            return TypedResults.NotFound();

        if (shelterAccount.Status != ShelterAccountStatus.Requested)
            return TypedResults.Conflict($"Cannot flag verification issues on a shelter account in status {shelterAccount.Status}.");

        var @event = shelterAccount.FlagVerificationIssues(request.Reason);
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new FlagVerificationIssuesResponse(shelterAccount.Id, shelterAccount.Status.ToString()));
    }
}
