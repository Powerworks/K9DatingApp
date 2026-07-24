using Marten;
using Microsoft.AspNetCore.Authorization;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.ReadModels.GetAdoptionListings;

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
///
/// v3 ENRICHMENT (Spec/K9CRUSH.emlang.v3.yaml's ShelterManagingListings
/// chapter): now filters to Status == Available. Before DogListing had a
/// Status field, every listing was implicitly adoptable; now that new
/// listings start NotReadyYet (see DogListing.Create) and approved ones
/// move to Adopted (see ApproveApplicationHandler), showing every
/// DogListing here regardless of status would surface dogs that aren't
/// actually open for applications.
/// </summary>
public static class GetAdoptionListingsHandler
{
    [WolverineGet("/api/v1/shelter-adoption/dog-listings")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<AdoptionListingsResponse> Handle(
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var listings = await session.Query<DogListing>()
            .Where(x => x.Status == DogListingStatus.Available)
            .ToListAsync(cancellationToken);

        var items = listings
            .Select(x => new AdoptionListingSummary(x.Id, x.Name, x.Breed, x.ShelterAccountId, x.PhotoIds))
            .ToList();

        return new AdoptionListingsResponse(items);
    }
}
