using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.PlaceDogInFoster;

/// <summary>
/// State-change slice: Spec/K9CRUSH.emlang.v3.yaml's FosteringADog
/// chapter, "Place Dog In Foster" -> "Dog Placed In Foster". Admin
/// policy, no ownership check needed - same reasoning as
/// ReviewFosterApplicationHandler.
///
/// Two guards: the DogListing must be Available or NotReadyYet (not
/// already InFoster/PendingAdoption/Adopted - can't foster a dog that's
/// already engaged elsewhere), and the referenced FosterApplication must
/// be Approved (not just any caller-supplied owner id - see
/// PlaceDogInFosterRequest's own comment).
/// </summary>
public static class PlaceDogInFosterHandler
{
    [WolverinePost("/api/v1/shelter-adoption/dog-listings/{dogListingId:guid}/place-in-foster")]
    [Authorize(Policy = "Admin")]
    public static async Task<Results<Ok<PlaceDogInFosterResponse>, NotFound, Conflict<string>>> Handle(
        Guid dogListingId,
        PlaceDogInFosterRequest request,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchForWriting<DogListing>(dogListingId, cancellationToken);
        var dogListing = stream.Aggregate;
        if (dogListing is null)
            return TypedResults.NotFound();

        if (dogListing.Status is not (DogListingStatus.Available or DogListingStatus.NotReadyYet))
            return TypedResults.Conflict($"Cannot place a dog in foster from listing status {dogListing.Status}.");

        var fosterApplication = await session.LoadAsync<FosterApplication>(request.FosterApplicationId, cancellationToken);
        if (fosterApplication is null)
            return TypedResults.NotFound();

        if (fosterApplication.Status != FosterApplicationStatus.Approved)
            return TypedResults.Conflict($"Cannot place a dog with a foster application in status {fosterApplication.Status}.");

        var @event = dogListing.PlaceInFoster(fosterApplication.ApplicantOwnerId);
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new PlaceDogInFosterResponse(dogListing.Id, dogListing.Status.ToString(), fosterApplication.ApplicantOwnerId));
    }
}
