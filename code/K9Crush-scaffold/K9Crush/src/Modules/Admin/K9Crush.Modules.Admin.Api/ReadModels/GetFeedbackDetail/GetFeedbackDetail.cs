namespace K9Crush.Modules.Admin.Api.ReadModels.GetFeedbackDetail;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record FeedbackDetailResponse(
    Guid FeedbackId,
    Guid OwnerId,
    string Message,
    DateTimeOffset SubmittedAt,
    string Status,
    string? ResponseMessage,
    DateTimeOffset? RespondedAt,
    DateTimeOffset? ResolvedAt);
