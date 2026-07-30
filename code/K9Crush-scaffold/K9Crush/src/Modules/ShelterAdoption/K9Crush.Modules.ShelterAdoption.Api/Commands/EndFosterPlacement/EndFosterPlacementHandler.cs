using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.EndFosterPlacement;

/// <summary>
/// State-change slice: Spec/K9CRUSH.emlang.v3.yaml's FosteringADog
/// chapter, "End Foster Placement" -> "Foster Placement Ended" - only
/// valid when a placement is actually active (CurrentFosterCaregiverOwnerId
/// is set), regardless of the current Status value (still InFoster, or
/// already flipped to Available via MarkFosterDogReadyForAdoption - see
/// DogListing.CurrentFosterCaregiverOwnerId's own comment for why it
/// outlives that transition). Admin policy, no ownership check needed -
/// same reasoning as ReviewFosterApplicationHandler.
/// </summary>
public static class EndFosterPlacementHandler
{
    [WolverinePost("/api/v1/shelter-adoption/dog-listings/{dogListingId:guid}/end-foster-placement")]
    [Authorize(Policy = "Admin")]
    public static async Task<Results<Ok<EndFosterPlacementResponse>, NotFound, Conflict<string>>> Handle(
        Guid dogListingId,
        EndFosterPlacementRequest request,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchForWriting<DogListing>(dogListingId, cancellationToken);
        var dogListing = stream.Aggregate;
        if (dogListing is null)
            return TypedResults.NotFound();

        if (dogListing.CurrentFosterCaregiverOwnerId is null)
            return TypedResults.Conflict("This listing has no active foster placement to end.");

        var @event = dogListing.EndFosterPlacement();
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new EndFosterPlacementResponse(dogListing.Id, dogListing.Status.ToString()));
    }
}
