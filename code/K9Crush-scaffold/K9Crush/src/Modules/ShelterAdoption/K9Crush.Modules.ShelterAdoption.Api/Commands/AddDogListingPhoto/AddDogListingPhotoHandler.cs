using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.AddDogListingPhoto;

/// <summary>
/// State-change slice: attaches an already-uploaded Media asset to a
/// DogListing. Ported from the removed Profiles module's AddDogProfilePhoto
/// (2026-07-24 descope, see DogListing.cs's own doc comment) - same
/// trust boundary as that slice: MediaAssetId is stored as-is, with no
/// cross-module call to Media to confirm it exists (matches this
/// codebase's existing convention for MediaAssetId references elsewhere).
///
/// Route is keyed by dogListingId alone (not nested under a
/// shelterAccountId route segment) - ownership is resolved by loading
/// the listing's own ShelterAccountId and checking that separately, same
/// pattern as EditDogListingHandler.
/// </summary>
public static class AddDogListingPhotoHandler
{
    [WolverinePost("/api/v1/shelter-adoption/dog-listings/{dogListingId:guid}/photos")]
    [Authorize(Policy = "Shelter")]
    public static async Task<Results<Ok<AddDogListingPhotoResponse>, NotFound, ForbidHttpResult>> Handle(
        Guid dogListingId,
        AddDogListingPhotoRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var stream = await session.Events.FetchForWriting<DogListing>(dogListingId, cancellationToken);
        var dogListing = stream.Aggregate;
        if (dogListing is null)
            return TypedResults.NotFound();

        var shelterAccount = await session.LoadAsync<ShelterAccount>(dogListing.ShelterAccountId, cancellationToken);
        if (shelterAccount is null || shelterAccount.RequestedByOwnerId != callerOwnerId)
            return TypedResults.Forbid();

        var @event = dogListing.AttachPhoto(request.MediaAssetId);
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new AddDogListingPhotoResponse(dogListing.Id, dogListing.PhotoIds));
    }
}
