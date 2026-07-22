using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.DeclineDogSurrender;

/// <summary>
/// State-change slice: Spec/K9CRUSH.emlang.v3.yaml's SurrenderingYourDog
/// chapter, "Decline Dog Surrender" -> "Dog Surrender Declined" - only
/// valid from UnderReview. Admin policy, no ownership check needed - same
/// reasoning as ReviewSurrenderRequestHandler. No Notifications cascade -
/// the yaml doesn't show a `cascadedTo` prop on this event (unlike
/// RejectApplicationHandler's ApplicationRejectedV1), so none is invented.
/// </summary>
public static class DeclineDogSurrenderHandler
{
    [WolverinePost("/api/v1/shelter-adoption/surrender-requests/{surrenderRequestId:guid}/decline")]
    [Authorize(Policy = "Admin")]
    public static async Task<Results<Ok<DeclineDogSurrenderResponse>, NotFound, Conflict<string>>> Handle(
        Guid surrenderRequestId,
        DeclineDogSurrenderRequest request,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var surrenderRequest = await session.LoadAsync<DogSurrenderRequest>(surrenderRequestId, cancellationToken);
        if (surrenderRequest is null)
            return TypedResults.NotFound();

        if (surrenderRequest.Status != SurrenderRequestStatus.UnderReview)
            return TypedResults.Conflict($"Cannot decline a surrender request in status {surrenderRequest.Status}.");

        surrenderRequest.Decline(request.Reason);
        session.Store(surrenderRequest);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new DeclineDogSurrenderResponse(surrenderRequest.Id, surrenderRequest.Status.ToString()));
    }
}
