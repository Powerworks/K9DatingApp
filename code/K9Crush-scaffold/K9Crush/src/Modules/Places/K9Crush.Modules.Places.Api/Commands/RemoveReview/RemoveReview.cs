namespace K9Crush.Modules.Places.Api.Commands.RemoveReview;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record RemoveReviewResponse(Guid ReviewId, string Status);
