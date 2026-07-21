namespace K9Crush.Modules.Places.Api.Commands.PublishReview;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record PublishReviewResponse(Guid ReviewId, string Status);
