using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using PawMatch.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace PawMatch.Modules.ShelterAdoption.Api.Commands.AddDogListing;

/// <summary>
/// State-change slice: SCREEN/CALLER -> COMMAND -> stores a new
/// DogListing under the given shelter account.
///
/// Covers both TheShelterRescueOrgSigningUp's "Add First Dog Listing" ->
/// "First Dog Listing Added" (this build session's current chapter) and
/// ShelterManagingListings' "Add Dog Listing" -> "Additional Dog Listing
/// Added" (a later chapter, not yet built) as one implementation - both
/// are the exact same technical action (create a DogListing for a
/// ShelterAccount), the "first vs. additional" distinction is just
/// narrative framing on the event-modeling board depending on whether
/// any other listings already exist for that shelter, not something the
/// command itself needs to branch on. Same consolidation call made for
/// ShelterAccount.Verify()/Activate() - see those comments for the
/// precedent.
///
/// Only valid once the shelter account is actually Created (activated) -
/// matches the emlang yaml's precondition ("given ShelterAccountCreated,
/// when AddFirstDogListing").
///
/// Now gated by the Shelter policy (ADR-017 role) plus an explicit
/// ownership check - role alone isn't enough, since a Shelter-role
/// caller should still only manage their own ShelterAccount's resources,
/// not every shelter's. Shelter role itself only exists via
/// PromoteOwnerToShelterOnAccountCreated (Identity module), triggered by
/// this same ShelterAccount reaching Created - so by construction, only
/// the shelter that was actually activated can ever satisfy both checks
/// together (barring a caller somehow reusing a stale/reassigned
/// shelterAccountId, which the ownership check still catches).
/// </summary>
public static class AddDogListingHandler
{
    [WolverinePost("/api/v1/shelter-adoption/shelter-accounts/{shelterAccountId:guid}/dog-listings")]
    [Authorize(Policy = "Shelter")]
    public static async Task<Results<Ok<AddDogListingResponse>, NotFound, ForbidHttpResult, Conflict<string>>> Handle(
        Guid shelterAccountId,
        AddDogListingRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var shelterAccount = await session.LoadAsync<ShelterAccount>(shelterAccountId, cancellationToken);
        if (shelterAccount is null)
            return TypedResults.NotFound();

        if (shelterAccount.RequestedByOwnerId != callerOwnerId)
            return TypedResults.Forbid();

        if (shelterAccount.Status != ShelterAccountStatus.Created)
            return TypedResults.Conflict($"Cannot add a dog listing to a shelter account in status {shelterAccount.Status}.");

        var dogListing = DogListing.Create(shelterAccountId, request.Name, request.Breed, request.AgeInMonths, request.Bio);
        session.Store(dogListing);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new AddDogListingResponse(dogListing.Id));
    }
}
