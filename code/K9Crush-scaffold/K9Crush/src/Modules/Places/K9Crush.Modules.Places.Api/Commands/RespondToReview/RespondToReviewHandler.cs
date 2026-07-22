using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Places.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Places.Api.Commands.RespondToReview;

/// <summary>
/// State-change slice: the emlang yaml's LeaveAReviewRestaurantOrDogPark
/// chapter's "Respond To Review" -> "Review Response Posted" - only
/// valid from Published. Gated by ownership of the reviewed Place (the
/// yaml's responderRole prop implies the business is responding to a
/// review of themselves) - not a Shelter/Admin-style role check, since
/// ClaimABusinessListing's real ownership-verification workflow doesn't
/// exist yet (see Place.cs's doc comment); Place.OwnerId is the only
/// notion of "who owns this place" this increment has.
/// </summary>
public static class RespondToReviewHandler
{
    [WolverinePost("/api/v1/places/reviews/{reviewId:guid}/respond")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<RespondToReviewResponse>, NotFound, ForbidHttpResult, Conflict<string>>> Handle(
        Guid reviewId,
        RespondToReviewRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var review = await session.LoadAsync<Review>(reviewId, cancellationToken);
        if (review is null)
            return TypedResults.NotFound();

        var place = await session.LoadAsync<Place>(review.PlaceId, cancellationToken);
        if (place is null || place.OwnerId != callerOwnerId)
            return TypedResults.Forbid();

        if (review.Status != ReviewStatus.Published)
            return TypedResults.Conflict($"Cannot respond to a review in status {review.Status}.");

        review.Respond(request.ResponseText, request.ResponderRole);
        session.Store(review);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new RespondToReviewResponse(review.Id, review.ResponseText!, review.ResponderRole!.Value.ToString()));
    }
}
