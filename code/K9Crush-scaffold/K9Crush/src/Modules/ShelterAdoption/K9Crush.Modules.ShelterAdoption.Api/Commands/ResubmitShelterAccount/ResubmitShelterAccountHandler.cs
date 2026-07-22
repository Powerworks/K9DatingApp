using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.ResubmitShelterAccount;

/// <summary>
/// State-change slice: the emlang yaml's "Resubmit Shelter Account
/// Request" -> "Shelter Account Request Resubmitted" - only valid from
/// VerificationIssuesFound. Resets status to Requested (see
/// ShelterAccount.Resubmit()'s comment), so VerifyShelterHandler needs no
/// changes to accept the re-verification that follows.
///
/// Stays on VerifiedOwner, not Admin - this endpoint's caller SHOULD be
/// the requesting shelter fixing their own flagged application, and at
/// this point in the lifecycle they aren't Shelter-role yet either (that
/// only happens once ShelterAccount actually activates). What closes the
/// gap here is the explicit ownership check below
/// (caller.OwnerId == shelterAccount.RequestedByOwnerId), same pattern as
/// SwipeOnDogHandler's ownership check against DiscoveryFeedItem.OwnerId.
/// </summary>
public static class ResubmitShelterAccountHandler
{
    [WolverinePost("/api/v1/shelter-adoption/shelter-accounts/{shelterAccountId:guid}/resubmit")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<ResubmitShelterAccountResponse>, NotFound, ForbidHttpResult, Conflict<string>>> Handle(
        Guid shelterAccountId,
        ResubmitShelterAccountRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var shelterAccount = await session.LoadAsync<ShelterAccount>(shelterAccountId, cancellationToken);
        if (shelterAccount is null)
            return TypedResults.NotFound();

        if (shelterAccount.RequestedByOwnerId != callerOwnerId)
            return TypedResults.Forbid();

        if (shelterAccount.Status != ShelterAccountStatus.VerificationIssuesFound)
            return TypedResults.Conflict($"Cannot resubmit a shelter account in status {shelterAccount.Status}.");

        shelterAccount.Resubmit(request.BusinessDetails, request.UtilityBillDocumentId);
        session.Store(shelterAccount);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new ResubmitShelterAccountResponse(shelterAccount.Id, shelterAccount.Status.ToString()));
    }
}
