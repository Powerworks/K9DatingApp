using System.ComponentModel.DataAnnotations;

namespace K9Crush.Modules.Places.Api.Commands.EditReview;

/// <summary>The request/command for this slice - what the caller sends.</summary>
public sealed record EditReviewRequest(
    [property: Range(1, 5)] int Rating,
    [property: Required, MaxLength(2000)] string Body);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record EditReviewResponse(Guid ReviewId, int Rating, string Body);
