using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.RequestAdditionalSurrenderDetails;

/// <summary>
/// State-change slice: Spec/K9CRUSH.emlang.v3.yaml's SurrenderingYourDog
/// chapter, "Request Additional Surrender Details" -> "Additional
/// Surrender Details Requested" - only valid from UnderReview. Admin
/// policy, no ownership check needed - same reasoning as
/// ReviewSurrenderRequestHandler.
///
/// Unlike RequestAdditionalDetailsHandler (Application), no scheduled
/// staleness clock - SurrenderRequestStatus has no Stale/Closed pair,
/// ADR-026's time-based automation is specific to
/// ShelterReviewsApplication and not modeled anywhere in this chapter.
/// </summary>
public static class RequestAdditionalSurrenderDetailsHandler
{
    [WolverinePost("/api/v1/shelter-adoption/surrender-requests/{surrenderRequestId:guid}/request-additional-details")]
    [Authorize(Policy = "Admin")]
    public static async Task<Results<Ok<RequestAdditionalSurrenderDetailsResponse>, NotFound, Conflict<string>>> Handle(
        Guid surrenderRequestId,
        RequestAdditionalSurrenderDetailsRequest request,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchForWriting<DogSurrenderRequest>(surrenderRequestId, cancellationToken);
        var surrenderRequest = stream.Aggregate;
        if (surrenderRequest is null)
            return TypedResults.NotFound();

        if (surrenderRequest.Status != SurrenderRequestStatus.UnderReview)
            return TypedResults.Conflict($"Cannot request additional details on a surrender request in status {surrenderRequest.Status}.");

        var @event = surrenderRequest.RequestAdditionalDetails(request.Reason);
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new RequestAdditionalSurrenderDetailsResponse(surrenderRequest.Id, surrenderRequest.Status.ToString()));
    }
}
