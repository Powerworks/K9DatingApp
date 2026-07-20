using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Discovery.Domain;
using K9Crush.Modules.Discovery.Domain.Events;
using Wolverine.Http;

namespace K9Crush.Modules.Discovery.Api.Commands.UndoLastSwipe;

/// <summary>
/// State-change slice: COMMAND -> EVENT. Undoes the caller's own most
/// recent swipe (like or pass) against a specific dog, for the
/// accidental-swipe case. Ownership is checked the same way
/// SwipeOnDogHandler checks it - against this module's own
/// DiscoveryFeedItem read model, never trusting SwiperDogId from the
/// request body alone.
///
/// "Last swipe" is computed live via
/// <c>AggregateStreamAsync&lt;UndoLastSwipeState&gt;</c> (ADR-019) - no
/// persisted snapshot. If the caller's side of the pair stream has no
/// currently-active (i.e. not already undone) swipe, this is rejected as
/// a Conflict rather than silently no-opping, since "nothing to undo" is
/// a real, named error scenario (slice.json spec-2), not a success case.
///
/// Deliberately does not touch DetectMutualMatch/MatchFormed - undoing a
/// swipe after a mutual match already formed is out of scope for this
/// slice (not in slice.json's specifications), matching the discipline of
/// building only what's specified rather than inventing further business
/// rules.
/// </summary>
public static class UndoLastSwipeHandler
{
    [WolverinePost("/api/v1/discovery/swipe/undo")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<UndoLastSwipeResponse>, NotFound, ForbidHttpResult, Conflict<string>>> Handle(
        UndoLastSwipeRequest request,
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
        var state = await session.Events.AggregateStreamAsync<UndoLastSwipeState>(streamId, token: cancellationToken);

        if (state is null || !state.HasActiveSwipeFor(request.SwiperDogId))
            return TypedResults.Conflict("No swipe to undo.");

        session.Events.Append(streamId, new SwipeUndone(request.SwiperDogId, request.TargetDogId, DateTimeOffset.UtcNow));
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new UndoLastSwipeResponse(Acknowledged: true));
    }
}
