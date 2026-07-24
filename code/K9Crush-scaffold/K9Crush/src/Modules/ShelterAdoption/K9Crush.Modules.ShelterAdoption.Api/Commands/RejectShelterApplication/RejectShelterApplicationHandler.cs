using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.RejectShelterApplication;

/// <summary>
/// State-change slice: the emlang yaml's "Reject Shelter Application" ->
/// "Shelter Application Rejected" - only valid from
/// VerificationIssuesFound (see ShelterAccount.Reject()'s comment for why,
/// per the yaml's GWT test preconditions). Terminal state - unlike
/// FlagVerificationIssues, there's no resubmission path back from Rejected
/// in the emlang yaml.
///
/// Now gated by the Admin policy - see VerifyShelterHandler's comment,
/// not repeated here.
/// </summary>
public static class RejectShelterApplicationHandler
{
    [WolverinePost("/api/v1/shelter-adoption/shelter-accounts/{shelterAccountId:guid}/reject")]
    [Authorize(Policy = "Admin")]
    public static async Task<Results<Ok<RejectShelterApplicationResponse>, NotFound, Conflict<string>>> Handle(
        Guid shelterAccountId,
        RejectShelterApplicationRequest request,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchForWriting<ShelterAccount>(shelterAccountId, cancellationToken);
        var shelterAccount = stream.Aggregate;
        if (shelterAccount is null)
            return TypedResults.NotFound();

        if (shelterAccount.Status != ShelterAccountStatus.VerificationIssuesFound)
            return TypedResults.Conflict($"Cannot reject a shelter application in status {shelterAccount.Status}.");

        var @event = shelterAccount.Reject(request.Reason);
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new RejectShelterApplicationResponse(shelterAccount.Id, shelterAccount.Status.ToString()));
    }
}
