using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.ReadModels.GetShelterDogListings;

/// <summary>
/// State-view slice: EVENT(s) -> READMODEL -> SCREEN. Direct document
/// query over DogListing (no projector needed - not event-sourced), same
/// shape as GetDogProfileHandler/OwnerAccountViewHandler.
///
/// Covers both the emlang yaml's "View Dog Listings" command+event pair
/// and the separate "Shelter Dog Listings" read-model view as one slice -
/// same consolidation already applied throughout this build-out
/// (GetDogProfileHandler, GetDiscoveryFeedHandler, OwnerAccountViewHandler
/// never got a separate "trivial view command" either; a page-load
/// command+event pair collapses into the state-view lane itself).
///
/// Gated by Shelter policy + ownership check, same pattern as
/// AddDogListingHandler - a shelter should only see its own listings
/// through this endpoint (a public "browse this shelter's listings" view
/// for adopters is TheWouldBeAdopter's concern, a different slice).
/// </summary>
public static class GetShelterDogListingsHandler
{
    [WolverineGet("/api/v1/shelter-adoption/shelter-accounts/{shelterAccountId:guid}/dog-listings")]
    [Authorize(Policy = "Shelter")]
    public static async Task<Results<Ok<ShelterDogListingsResponse>, NotFound, ForbidHttpResult>> Handle(
        Guid shelterAccountId,
        ClaimsPrincipal user,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var shelterAccount = await session.LoadAsync<ShelterAccount>(shelterAccountId, cancellationToken);
        if (shelterAccount is null)
            return TypedResults.NotFound();

        if (shelterAccount.RequestedByOwnerId != callerOwnerId)
            return TypedResults.Forbid();

        var listings = await session.Query<DogListing>()
            .Where(x => x.ShelterAccountId == shelterAccountId)
            .ToListAsync(cancellationToken);

        var items = listings
            .Select(x => new DogListingSummary(x.Id, x.Name, x.Breed, x.AgeInMonths))
            .ToList();

        return TypedResults.Ok(new ShelterDogListingsResponse(items));
    }
}
