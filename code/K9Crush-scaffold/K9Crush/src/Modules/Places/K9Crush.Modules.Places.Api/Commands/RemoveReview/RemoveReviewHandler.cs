using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Places.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Places.Api.Commands.RemoveReview;

/// <summary>
/// State-change slice: the emlang yaml's LeaveAReviewRestaurantOrDogPark
/// chapter's "Remove Review" -> "Review Removed" - only valid from
/// Published, a soft delete (see Review.Remove()'s doc comment for why).
/// Ownership-gated to the reviewer.
/// </summary>
public static class RemoveReviewHandler
{
    [WolverinePost("/api/v1/places/reviews/{reviewId:guid}/remove")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<RemoveReviewResponse>, NotFound, ForbidHttpResult, Conflict<string>>> Handle(
        Guid reviewId,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var review = await session.LoadAsync<Review>(reviewId, cancellationToken);
        if (review is null)
            return TypedResults.NotFound();

        if (review.ReviewerOwnerId != callerOwnerId)
            return TypedResults.Forbid();

        if (review.Status != ReviewStatus.Published)
            return TypedResults.Conflict($"Cannot remove a review in status {review.Status}.");

        review.Remove();
        session.Store(review);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new RemoveReviewResponse(review.Id, review.Status.ToString()));
    }
}
