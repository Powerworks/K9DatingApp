using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.ReadModels.GetDogListingDetails;

/// <summary>
/// State-view slice: EVENT(s) -> READMODEL -> SCREEN. Covers the emlang
/// yaml's "View Dog Details" command+event pair and the "Dog Profile"
/// view as one slice, same consolidation applied throughout this
/// build-out. Public/member-facing, no ownership filter - same reasoning
/// as GetAdoptionListingsHandler.
/// </summary>
public static class GetDogListingDetailsHandler
{
    [WolverineGet("/api/v1/shelter-adoption/dog-listings/{dogListingId:guid}")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<DogListingDetailsResponse>, NotFound>> Handle(
        Guid dogListingId,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var dogListing = await session.LoadAsync<DogListing>(dogListingId, cancellationToken);
        if (dogListing is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(new DogListingDetailsResponse(
            dogListing.Id,
            dogListing.Name,
            dogListing.Breed,
            dogListing.AgeInMonths,
            dogListing.Bio,
            dogListing.ShelterAccountId,
            dogListing.Status));
    }
}
