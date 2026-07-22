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
/// NotReadyYet while a new intake settles in, or InFoster/Available/
/// PendingAdoption corrections).
///
/// Adopted is deliberately off-limits to this endpoint in both
/// directions - not a settable target (Adopted must only ever be reached
/// via an actually-approved Application, never asserted by hand) and not
/// editable once reached (a genuinely adopted listing doesn't get
/// silently cycled back into the availability pool by a manual status
/// flip - a real "dog returned after adoption" flow, if this product
/// ever needs one, is its own command/chapter, not a side effect of this
/// one). Found via an event-modeling checklist pass against
/// Spec/K9CRUSH.emlang.v3.yaml (2026-07-22) - the yaml itself doesn't
/// define valid status transitions either; this guard is the fix on both
/// sides.
///
/// Route/ownership-gate pattern matches EditDogListingHandler - keyed by
/// dogListingId alone, ownership resolved via the listing's own
/// ShelterAccountId.
/// </summary>
public static class UpdateListingStatusHandler
{
    [WolverinePost("/api/v1/shelter-adoption/dog-listings/{dogListingId:guid}/status")]
    [Authorize(Policy = "Shelter")]
    public static async Task<Results<Ok<UpdateListingStatusResponse>, NotFound, ForbidHttpResult, Conflict<string>>> Handle(
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

        if (dogListing.Status == DogListingStatus.Adopted || request.Status == DogListingStatus.Adopted)
            return TypedResults.Conflict("Adopted can only be reached via an approved Application, and cannot be changed once reached.");

        dogListing.UpdateStatus(request.Status);
        session.Store(dogListing);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new UpdateListingStatusResponse(dogListing.Id, dogListing.Status));
    }
}
