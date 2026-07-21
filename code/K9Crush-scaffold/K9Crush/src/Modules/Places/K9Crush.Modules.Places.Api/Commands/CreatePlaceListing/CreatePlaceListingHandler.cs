using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using K9Crush.Modules.Places.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Places.Api.Commands.CreatePlaceListing;

/// <summary>
/// State-change slice: a disclosed gap-fill, not a named yaml command -
/// see Place.cs's own doc comment for why this needs to exist even
/// though the yaml's own ClaimABusinessListing chapter assumes listings
/// already exist. The creating caller becomes the Place's OwnerId
/// directly - no claim/verification workflow.
/// </summary>
public static class CreatePlaceListingHandler
{
    [WolverinePost("/api/v1/places")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<CreatePlaceListingResponse> Handle(
        CreatePlaceListingRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var ownerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var place = Place.Create(ownerId, request.Name, request.PlaceType);
        session.Store(place);
        await session.SaveChangesAsync(cancellationToken);

        return new CreatePlaceListingResponse(place.Id);
    }
}
