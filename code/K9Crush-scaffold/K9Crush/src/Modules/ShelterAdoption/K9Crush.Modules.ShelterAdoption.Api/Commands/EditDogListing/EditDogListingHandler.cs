using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.EditDogListing;

/// <summary>
/// State-change slice: the emlang yaml's "Edit Dog Listing" -> "Dog
/// Listing Edited". Route is keyed by dogListingId alone (not nested
/// under a shelterAccountId route segment) - ownership is resolved by
/// loading the listing's own ShelterAccountId and checking that
/// separately, avoiding a redundant/confusable two-id route.
///
/// Gated by Shelter policy + ownership check, same pattern as
/// AddDogListingHandler/GetShelterDogListingsHandler.
/// </summary>
public static class EditDogListingHandler
{
    [WolverinePost("/api/v1/shelter-adoption/dog-listings/{dogListingId:guid}/edit")]
    [Authorize(Policy = "Shelter")]
    public static async Task<Results<Ok<EditDogListingResponse>, NotFound, ForbidHttpResult>> Handle(
        Guid dogListingId,
        EditDogListingRequest request,
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

        dogListing.Edit(request.Name, request.Breed, request.AgeInMonths, request.Bio);
        session.Store(dogListing);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new EditDogListingResponse(dogListing.Id));
    }
}
