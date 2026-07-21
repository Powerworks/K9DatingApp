using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Discovery.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Discovery.Api.Commands.BlockMatchAttempt;

/// <summary>
/// State-change slice: TheWindowShopper chapter's "Block Match Attempt" ->
/// "Match Attempt Blocked". Deliberately anonymous - no [Authorize] - this
/// is the Guest browsing the feed (see GetDiscoveryFeedHandler) trying to
/// act on a specific dog before signing up. Always blocks; there's no
/// swipe/match mechanism a Guest could ever satisfy without an account, so
/// this isn't a real precondition check beyond "does the dog exist" - it's
/// a deliberate UX gate prompting sign-up (see "Guest/Sign Up to Match"
/// screen in the yaml, immediately following this event).
/// </summary>
public static class BlockMatchAttemptHandler
{
    [WolverinePost("/api/v1/discovery/dogs/{dogId:guid}/match-attempt")]
    public static async Task<Results<Ok<BlockMatchAttemptResponse>, NotFound>> Handle(
        Guid dogId,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var dog = await session.LoadAsync<DiscoveryFeedItem>(dogId, cancellationToken);
        if (dog is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(new BlockMatchAttemptResponse(dogId, Reason: "sign_up_required"));
    }
}
