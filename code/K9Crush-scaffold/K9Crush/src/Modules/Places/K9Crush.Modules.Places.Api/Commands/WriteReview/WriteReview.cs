using System.ComponentModel.DataAnnotations;

namespace K9Crush.Modules.Places.Api.Commands.WriteReview;

/// <summary>The request/command for this slice - what the caller sends.</summary>
public sealed record WriteReviewRequest(
    [property: Range(1, 5)] int Rating,
    [property: Required, MaxLength(2000)] string Body,
    bool VisitVerificationRequired);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record WriteReviewResponse(Guid ReviewId, string Status);
