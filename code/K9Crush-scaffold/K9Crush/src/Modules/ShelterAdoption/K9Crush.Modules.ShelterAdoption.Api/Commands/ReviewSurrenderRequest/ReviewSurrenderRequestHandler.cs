using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.ReviewSurrenderRequest;

/// <summary>
/// State-change slice: Spec/K9CRUSH.emlang.v3.yaml's SurrenderingYourDog
/// chapter, "Review Surrender Request" -> "Surrender Request Reviewed" -
/// only valid from Requested. Admin policy, no ownership check needed -
/// same reasoning as CreateShelterAccountHandler/ApproveShelterAccountHandler
/// (Admin acts platform-wide, not scoped to any one shelter).
/// </summary>
public static class ReviewSurrenderRequestHandler
{
    [WolverinePost("/api/v1/shelter-adoption/surrender-requests/{surrenderRequestId:guid}/review")]
    [Authorize(Policy = "Admin")]
    public static async Task<Results<Ok<ReviewSurrenderRequestResponse>, NotFound, Conflict<string>>> Handle(
        Guid surrenderRequestId,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var surrenderRequest = await session.LoadAsync<DogSurrenderRequest>(surrenderRequestId, cancellationToken);
        if (surrenderRequest is null)
            return TypedResults.NotFound();

        if (surrenderRequest.Status != SurrenderRequestStatus.Requested)
            return TypedResults.Conflict($"Cannot review a surrender request in status {surrenderRequest.Status}.");

        surrenderRequest.Review();
        session.Store(surrenderRequest);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new ReviewSurrenderRequestResponse(surrenderRequest.Id, surrenderRequest.Status.ToString()));
    }
}
