using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Places.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Places.Api.Commands.EditReview;

/// <summary>
/// State-change slice: the emlang yaml's LeaveAReviewRestaurantOrDogPark
/// chapter's "Edit Review" -> "Review Edited" - only valid from Published
/// (per the yaml's own test, ReviewEdited's "given: Review Published").
/// Ownership-gated to the reviewer.
/// </summary>
public static class EditReviewHandler
{
    [WolverinePost("/api/v1/places/reviews/{reviewId:guid}/edit")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<EditReviewResponse>, NotFound, ForbidHttpResult, Conflict<string>>> Handle(
        Guid reviewId,
        EditReviewRequest request,
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
            return TypedResults.Conflict($"Cannot edit a review in status {review.Status}.");

        review.Edit(request.Rating, request.Body);
        session.Store(review);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new EditReviewResponse(review.Id, review.Rating, review.Body));
    }
}
