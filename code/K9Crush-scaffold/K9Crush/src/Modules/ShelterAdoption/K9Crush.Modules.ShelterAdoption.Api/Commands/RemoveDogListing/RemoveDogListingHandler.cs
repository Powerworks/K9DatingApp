using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Contracts;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.RemoveDogListing;

/// <summary>
/// State-change slice: the emlang yaml's "Remove Dog Listing" -> "Dog
/// Listing Removed" - a genuine document delete, not a soft-delete flag.
/// "removed" isn't one of DogListing's own future status values
/// (listed/pending_applications/adopted, per ShelterManagingListings'
/// read model), and nothing currently reads a removed listing (no
/// history needed) - see DogListing.cs's comment.
///
/// Now cascades DogListingRemovedV1 (ADR-028) - the trigger for
/// Automations/CancelApplicationsForRemovedListing (same module) and,
/// downstream of that, Notifications' cancellation email. DogName is
/// captured before the delete since nothing downstream can look it up
/// afterward.
///
/// Gated by Shelter policy + ownership check, same pattern as
/// EditDogListingHandler/AddDogListingHandler.
/// </summary>
public static class RemoveDogListingHandler
{
    [WolverinePost("/api/v1/shelter-adoption/dog-listings/{dogListingId:guid}/remove")]
    [Authorize(Policy = "Shelter")]
    public static async Task<(Results<Ok, NotFound, ForbidHttpResult>, DogListingRemovedV1?)> Handle(
        Guid dogListingId,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var dogListing = await session.LoadAsync<DogListing>(dogListingId, cancellationToken);
        if (dogListing is null)
            return (TypedResults.NotFound(), null);

        var shelterAccount = await session.LoadAsync<ShelterAccount>(dogListing.ShelterAccountId, cancellationToken);
        if (shelterAccount is null || shelterAccount.RequestedByOwnerId != callerOwnerId)
            return (TypedResults.Forbid(), null);

        var integrationEvent = new DogListingRemovedV1(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            DogListingId: dogListing.Id,
            ShelterAccountId: dogListing.ShelterAccountId,
            DogName: dogListing.Name);

        session.Delete(dogListing);
        await session.SaveChangesAsync(cancellationToken);

        return (TypedResults.Ok(), integrationEvent);
    }
}
