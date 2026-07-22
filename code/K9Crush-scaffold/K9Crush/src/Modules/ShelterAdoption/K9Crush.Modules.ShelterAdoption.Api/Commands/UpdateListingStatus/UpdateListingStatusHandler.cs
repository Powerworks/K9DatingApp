using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.UpdateListingStatus;

/// <summary>
/// State-change slice: the emlang yaml's "Update Listing Status" ->
/// "Listing Status Updated" (v3 ENRICHMENT, Spec/K9CRUSH.emlang.v3.yaml's
/// ShelterManagingListings chapter). Manual Shelter Staff override -
/// ApproveApplicationHandler already cascades Status to Adopted on
/// approval, and the not-yet-built FosteringADog chapter is planned to
/// cascade InFoster/Available around foster placements; this endpoint
/// covers everything else a shelter needs to set by hand (e.g.
/// NotReadyYet while a new intake settles in, or manually correcting a
/// listing that was Adopted via a route other than this app).
///
/// Route/ownership-gate pattern matches EditDogListingHandler - keyed by
/// dogListingId alone, ownership resolved via the listing's own
/// ShelterAccountId.
/// </summary>
public static class UpdateListingStatusHandler
{
    [WolverinePost("/api/v1/shelter-adoption/dog-listings/{dogListingId:guid}/status")]
    [Authorize(Policy = "Shelter")]
    public static async Task<Results<Ok<UpdateListingStatusResponse>, NotFound, ForbidHttpResult>> Handle(
        Guid dogListingId,
        UpdateListingStatusRequest request,
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

        dogListing.UpdateStatus(request.Status);
        session.Store(dogListing);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new UpdateListingStatusResponse(dogListing.Id, dogListing.Status));
    }
}
