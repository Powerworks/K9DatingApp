using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.RemoveDogListing;

/// <summary>
/// State-change slice: the emlang yaml's "Remove Dog Listing" -> "Dog
/// Listing Removed" - a genuine document delete, not a soft-delete flag.
/// "removed" isn't one of DogListing's own future status values
/// (listed/pending_applications/adopted, per ShelterManagingListings'
/// read model), and nothing currently reads a removed listing (no
/// Application entity exists yet to need "this listing used to exist"
/// history) - see DogListing.cs's comment.
///
/// Deliberately does NOT cover the emlang yaml's cascading automations
/// (CancelApplicationsForRemovedListing, NotifyApplicantsOfCancellation) -
/// both need an Application entity and a Notifications module that don't
/// exist yet. Add those when TheWouldBeAdopter/ShelterReviewsApplication
/// and Notifications are actually built.
///
/// Gated by Shelter policy + ownership check, same pattern as
/// EditDogListingHandler/AddDogListingHandler.
/// </summary>
public static class RemoveDogListingHandler
{
    [WolverinePost("/api/v1/shelter-adoption/dog-listings/{dogListingId:guid}/remove")]
    [Authorize(Policy = "Shelter")]
    public static async Task<Results<Ok, NotFound, ForbidHttpResult>> Handle(
        Guid dogListingId,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var dogListing = await session.LoadAsync<DogListing>(dogListingId, cancellationToken);
        if (dogListing is null)
            return TypedResults.NotFound();

        var shelterAccount = await session.LoadAsync<ShelterAccount>(dogListing.ShelterAccountId, cancellationToken);
        if (shelterAccount is null || shelterAccount.RequestedByOwnerId != callerOwnerId)
            return TypedResults.Forbid();

        session.Delete(dogListing);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok();
    }
}
