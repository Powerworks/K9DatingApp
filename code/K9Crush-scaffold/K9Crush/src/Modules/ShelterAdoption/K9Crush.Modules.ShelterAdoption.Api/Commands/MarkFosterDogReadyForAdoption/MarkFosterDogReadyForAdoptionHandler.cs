using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.MarkFosterDogReadyForAdoption;

/// <summary>
/// State-change slice: Spec/K9CRUSH.emlang.v3.yaml's FosteringADog
/// chapter, "Mark Foster Dog Ready For Adoption" -> "Foster Dog Marked
/// Ready For Adoption" - only valid from InFoster. Admin policy, no
/// ownership check needed - same reasoning as ReviewFosterApplicationHandler.
/// </summary>
public static class MarkFosterDogReadyForAdoptionHandler
{
    [WolverinePost("/api/v1/shelter-adoption/dog-listings/{dogListingId:guid}/mark-foster-dog-ready-for-adoption")]
    [Authorize(Policy = "Admin")]
    public static async Task<Results<Ok<MarkFosterDogReadyForAdoptionResponse>, NotFound, Conflict<string>>> Handle(
        Guid dogListingId,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var dogListing = await session.LoadAsync<DogListing>(dogListingId, cancellationToken);
        if (dogListing is null)
            return TypedResults.NotFound();

        if (dogListing.Status != DogListingStatus.InFoster)
            return TypedResults.Conflict($"Cannot mark ready for adoption from listing status {dogListing.Status}.");

        dogListing.MarkFosterDogReadyForAdoption();
        session.Store(dogListing);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new MarkFosterDogReadyForAdoptionResponse(dogListing.Id, dogListing.Status.ToString()));
    }
}
