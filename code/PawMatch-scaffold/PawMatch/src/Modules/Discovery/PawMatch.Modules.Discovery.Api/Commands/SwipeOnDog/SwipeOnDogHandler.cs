using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using PawMatch.Modules.Discovery.Domain;
using PawMatch.Modules.Discovery.Domain.Events;
using Wolverine.Http;

namespace PawMatch.Modules.Discovery.Api.Commands.SwipeOnDog;

public static class SwipeOnDogHandler
{
    /// <summary>
    /// Pure state-change slice: COMMAND -> EVENT(s). Appends exactly one
    /// event to the pair's stream and nothing else - no match detection,
    /// no cross-module event, no conditional branching on aggregate state
    /// beyond "does this stream exist yet." Match detection is a separate
    /// decision that lives in Automations/DetectMutualMatch, triggered by
    /// the DogLiked event this handler appends (see the Event Modeling
    /// blueprint doc for why these were split).
    ///
    /// Originally had no [Authorize] AND no check that the caller actually
    /// owns SwiperDogId - anyone could swipe as any dog. Fixed on the same
    /// pass as CreateDogProfileHandler's missing-auth bug. Ownership is
    /// verified against DiscoveryFeedItem (this module's own read model,
    /// already storing OwnerId per dog - no cross-module Domain reference
    /// needed, stays within the Contracts-only boundary rule) rather than
    /// trusting SwiperDogId from the request body.
    /// </summary>
    [WolverinePost("/api/v1/discovery/swipe")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<SwipeOnDogResponse>, NotFound, ForbidHttpResult>> Handle(
        SwipeOnDogRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var swiperDog = await session.LoadAsync<DiscoveryFeedItem>(request.SwiperDogId, cancellationToken);
        if (swiperDog is null)
            return TypedResults.NotFound();

        if (swiperDog.OwnerId != callerOwnerId)
            return TypedResults.Forbid();

        var streamId = MatchStream.IdFor(request.SwiperDogId, request.TargetDogId);
        var now = DateTimeOffset.UtcNow;

        object swipeEvent = request.Liked
            ? new DogLiked(request.SwiperDogId, request.TargetDogId, now)
            : new DogPassed(request.SwiperDogId, request.TargetDogId, now);

        session.Events.Append(streamId, swipeEvent);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new SwipeOnDogResponse(Acknowledged: true));
    }
}
