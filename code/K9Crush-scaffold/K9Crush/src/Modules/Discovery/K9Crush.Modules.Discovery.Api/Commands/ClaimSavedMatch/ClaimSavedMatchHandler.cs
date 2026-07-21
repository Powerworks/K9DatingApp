using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Discovery.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Discovery.Api.Commands.ClaimSavedMatch;

/// <summary>
/// State-change slice: TheWindowShopper chapter's "Claim Saved Match" ->
/// "Saved Match Claimed", except when the dog is no longer available,
/// which the yaml names as its own outcome ("Reject Claim (Match No
/// Longer Available)" -> "Saved Match No Longer Available") rather than a
/// generic conflict - same "keep the yaml's named outcome visible"
/// pattern as WithdrawApplicationHandler's "Withdrawal Blocked: Already
/// Approved".
///
/// The dogId here is one the client remembered from before the caller
/// signed up (from the earlier BlockMatchAttemptHandler response, while
/// still a Guest) and passes back once Profile Confirmed - no
/// server-side guest-session tracking, per the event-modeling call made
/// for this chapter. Shares its actual mechanism (DogOfInterest.Flag)
/// with FlagDogOfInterestHandler - see DogOfInterest.cs's doc comment.
/// </summary>
public static class ClaimSavedMatchHandler
{
    [WolverinePost("/api/v1/discovery/dogs/{dogId:guid}/claim-saved-match")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<ClaimSavedMatchResponse>, Conflict<string>>> Handle(
        Guid dogId,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var ownerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var dog = await session.LoadAsync<DiscoveryFeedItem>(dogId, cancellationToken);
        if (dog is null)
            return TypedResults.Conflict("Saved Match No Longer Available.");

        var dogOfInterest = DogOfInterest.Flag(ownerId, dogId);
        session.Store(dogOfInterest);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new ClaimSavedMatchResponse(dogId));
    }
}
