using Marten;
using Microsoft.AspNetCore.Authorization;
using PawMatch.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace PawMatch.Modules.ShelterAdoption.Api.ReadModels.GetAdoptionListings;

/// <summary>
/// State-view slice: EVENT(s) -> READMODEL -> SCREEN. Covers the emlang
/// yaml's "Browse Adoption Listings" command+event pair and the
/// "Adoption Listings" view as one slice, same consolidation applied
/// throughout this build-out.
///
/// Unlike GetShelterDogListingsHandler (a shelter viewing its OWN
/// listings, ownership-gated), this is the public/member-facing
/// marketplace browse - every DogListing across every shelter, no
/// ownership filter. No extra "is this shelter active" filter needed
/// either: AddDogListingHandler already requires the owning
/// ShelterAccount to be Created before a listing can exist at all, so
/// every DogListing in the store already belongs to an active shelter.
///
/// VerifiedOwner only (any member can browse) - no Shelter role needed.
/// </summary>
public static class GetAdoptionListingsHandler
{
    [WolverineGet("/api/v1/shelter-adoption/dog-listings")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<AdoptionListingsResponse> Handle(
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var listings = await session.Query<DogListing>().ToListAsync(cancellationToken);

        var items = listings
            .Select(x => new AdoptionListingSummary(x.Id, x.Name, x.Breed, x.ShelterAccountId))
            .ToList();

        return new AdoptionListingsResponse(items);
    }
}
