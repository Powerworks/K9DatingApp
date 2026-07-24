using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.ReviewFosterApplication;

/// <summary>
/// State-change slice: Spec/K9CRUSH.emlang.v3.yaml's FosteringADog
/// chapter, "Review Foster Application" -> "Foster Application Reviewed"
/// - only valid from Submitted. Admin policy, no ownership check needed -
/// same reasoning as ReviewSurrenderRequestHandler (foster program
/// administration is Admin-level per the yaml's own swimlane, not scoped
/// to any one shelter).
/// </summary>
public static class ReviewFosterApplicationHandler
{
    [WolverinePost("/api/v1/shelter-adoption/foster-applications/{fosterApplicationId:guid}/review")]
    [Authorize(Policy = "Admin")]
    public static async Task<Results<Ok<ReviewFosterApplicationResponse>, NotFound, Conflict<string>>> Handle(
        Guid fosterApplicationId,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchForWriting<FosterApplication>(fosterApplicationId, cancellationToken);
        var fosterApplication = stream.Aggregate;
        if (fosterApplication is null)
            return TypedResults.NotFound();

        if (fosterApplication.Status != FosterApplicationStatus.Submitted)
            return TypedResults.Conflict($"Cannot review a foster application in status {fosterApplication.Status}.");

        var @event = fosterApplication.Review();
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new ReviewFosterApplicationResponse(fosterApplication.Id, fosterApplication.Status.ToString()));
    }
}
