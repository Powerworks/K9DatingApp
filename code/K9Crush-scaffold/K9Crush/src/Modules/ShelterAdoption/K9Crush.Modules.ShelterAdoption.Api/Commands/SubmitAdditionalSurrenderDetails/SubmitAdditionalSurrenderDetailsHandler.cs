using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.SubmitAdditionalSurrenderDetails;

/// <summary>
/// State-change slice: Spec/K9CRUSH.emlang.v3.yaml's SurrenderingYourDog
/// chapter, "Submit Additional Surrender Details" -> "Additional
/// Surrender Details Submitted" - only valid from
/// AdditionalDetailsRequested. Ownership-gated to the surrendering member
/// (VerifiedOwner + caller == RequestedByOwnerId) - already correctly
/// labeled "Member" in the yaml, unlike Application's equivalent step (no
/// actor-label departure needed here).
///
/// No request body - same reasoning as SubmitAdditionalDetailsHandler
/// (Application): the yaml doesn't specify what "additional details"
/// content looks like beyond the reason text already captured on the
/// request side.
/// </summary>
public static class SubmitAdditionalSurrenderDetailsHandler
{
    [WolverinePost("/api/v1/shelter-adoption/surrender-requests/{surrenderRequestId:guid}/submit-additional-details")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<SubmitAdditionalSurrenderDetailsResponse>, NotFound, ForbidHttpResult, Conflict<string>>> Handle(
        Guid surrenderRequestId,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var surrenderRequest = await session.LoadAsync<DogSurrenderRequest>(surrenderRequestId, cancellationToken);
        if (surrenderRequest is null)
            return TypedResults.NotFound();

        if (surrenderRequest.RequestedByOwnerId != callerOwnerId)
            return TypedResults.Forbid();

        if (surrenderRequest.Status != SurrenderRequestStatus.AdditionalDetailsRequested)
            return TypedResults.Conflict($"Cannot submit additional details on a surrender request in status {surrenderRequest.Status}.");

        surrenderRequest.SubmitAdditionalDetails();
        session.Store(surrenderRequest);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new SubmitAdditionalSurrenderDetailsResponse(surrenderRequest.Id, surrenderRequest.Status.ToString()));
    }
}
