using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Places.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Places.Api.Commands.WriteReview;

/// <summary>
/// State-change slice: the emlang yaml's LeaveAReviewRestaurantOrDogPark
/// chapter's "Write Review" -> "Review Written" - a Draft, not yet
/// visible (see PublishReviewHandler). No ownership/role gate beyond
/// VerifiedOwner - any member can write a review for any place.
/// </summary>
public static class WriteReviewHandler
{
    [WolverinePost("/api/v1/places/{placeId:guid}/reviews")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<WriteReviewResponse>, NotFound>> Handle(
        Guid placeId,
        WriteReviewRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var reviewerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var place = await session.LoadAsync<Place>(placeId, cancellationToken);
        if (place is null)
            return TypedResults.NotFound();

        var review = Review.Write(placeId, reviewerOwnerId, request.Rating, request.Body, request.VisitVerificationRequired);
        session.Store(review);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new WriteReviewResponse(review.Id, review.Status.ToString()));
    }
}
