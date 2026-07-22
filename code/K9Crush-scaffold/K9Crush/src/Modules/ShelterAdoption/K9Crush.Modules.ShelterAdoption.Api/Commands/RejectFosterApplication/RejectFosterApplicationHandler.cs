using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.RejectFosterApplication;

/// <summary>
/// State-change slice: Spec/K9CRUSH.emlang.v3.yaml's FosteringADog
/// chapter, "Reject Foster Application" -> "Foster Application Rejected"
/// - only valid from UnderReview. Admin policy, no ownership check needed
/// - same reasoning as ReviewFosterApplicationHandler.
/// </summary>
public static class RejectFosterApplicationHandler
{
    [WolverinePost("/api/v1/shelter-adoption/foster-applications/{fosterApplicationId:guid}/reject")]
    [Authorize(Policy = "Admin")]
    public static async Task<Results<Ok<RejectFosterApplicationResponse>, NotFound, Conflict<string>>> Handle(
        Guid fosterApplicationId,
        RejectFosterApplicationRequest request,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var fosterApplication = await session.LoadAsync<FosterApplication>(fosterApplicationId, cancellationToken);
        if (fosterApplication is null)
            return TypedResults.NotFound();

        if (fosterApplication.Status != FosterApplicationStatus.UnderReview)
            return TypedResults.Conflict($"Cannot reject a foster application in status {fosterApplication.Status}.");

        fosterApplication.Reject(request.Reason);
        session.Store(fosterApplication);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new RejectFosterApplicationResponse(fosterApplication.Id, fosterApplication.Status.ToString()));
    }
}
