using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Places.Contracts;
using K9Crush.Modules.Places.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Places.Api.Commands.ReportReview;

/// <summary>
/// State-change slice: the emlang yaml's LeaveAReviewRestaurantOrDogPark
/// chapter's "Report Review" -> "Content Flagged". Deliberately NOT
/// ownership-gated - same reasoning as Media's ReportMediaHandler,
/// reporting is something any other member does, not the reviewer's own
/// action. Cascades ReviewContentFlaggedV1 - see that contract's own doc
/// comment for how it feeds into Moderation.
/// </summary>
public static class ReportReviewHandler
{
    [WolverinePost("/api/v1/places/reviews/{reviewId:guid}/report")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<(Results<Ok<ReportReviewResponse>, NotFound>, ReviewContentFlaggedV1?)> Handle(
        Guid reviewId,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var reporterOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var review = await session.LoadAsync<Review>(reviewId, cancellationToken);
        if (review is null)
            return (TypedResults.NotFound(), null);

        var integrationEvent = new ReviewContentFlaggedV1(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            ReviewId: review.Id,
            ContentOwnerId: review.ReviewerOwnerId,
            ReporterOwnerId: reporterOwnerId);

        return (TypedResults.Ok(new ReportReviewResponse(review.Id)), integrationEvent);
    }
}
