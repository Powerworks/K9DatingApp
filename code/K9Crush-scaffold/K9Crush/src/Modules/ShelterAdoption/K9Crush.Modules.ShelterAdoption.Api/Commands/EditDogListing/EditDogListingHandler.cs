using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Contracts;
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
/// Now cascades DogListingSignificantlyEditedV1 (ADR-028) when the caller
/// flags SignificantChange - the trigger for
/// Automations/NotifyApplicantsOfListingChange (same module). No
/// cascade at all when SignificantChange is false; that's not an error,
/// just nothing further to do.
///
/// Gated by Shelter policy + ownership check, same pattern as
/// AddDogListingHandler/GetShelterDogListingsHandler.
/// </summary>
public static class EditDogListingHandler
{
    [WolverinePost("/api/v1/shelter-adoption/dog-listings/{dogListingId:guid}/edit")]
    [Authorize(Policy = "Shelter")]
    public static async Task<(Results<Ok<EditDogListingResponse>, NotFound, ForbidHttpResult>, DogListingSignificantlyEditedV1?)> Handle(
        Guid dogListingId,
        EditDogListingRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var stream = await session.Events.FetchForWriting<DogListing>(dogListingId, cancellationToken);
        var dogListing = stream.Aggregate;
        if (dogListing is null)
            return (TypedResults.NotFound(), null);

        var shelterAccount = await session.LoadAsync<ShelterAccount>(dogListing.ShelterAccountId, cancellationToken);
        if (shelterAccount is null || shelterAccount.RequestedByOwnerId != callerOwnerId)
            return (TypedResults.Forbid(), null);

        var editedEvent = dogListing.Edit(request.Name, request.Breed, request.AgeInMonths, request.Bio);
        stream.AppendOne(editedEvent);
        await session.SaveChangesAsync(cancellationToken);

        var integrationEvent = request.SignificantChange
            ? new DogListingSignificantlyEditedV1(
                EventId: Guid.NewGuid(),
                OccurredAt: DateTimeOffset.UtcNow,
                DogListingId: dogListing.Id,
                ShelterAccountId: dogListing.ShelterAccountId,
                DogName: dogListing.Name)
            : null;

        return (TypedResults.Ok(new EditDogListingResponse(dogListing.Id)), integrationEvent);
    }
}
