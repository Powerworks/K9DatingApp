namespace K9Crush.Modules.Admin.Api.ReadModels.GetFeedbackInbox;

/// <summary>One row in the inbox listing - a summary, not the full detail (see GetFeedbackDetail for that).</summary>
public sealed record FeedbackInboxEntry(
    Guid FeedbackId,
    Guid OwnerId,
    string Message,
    DateTimeOffset SubmittedAt,
    string Status);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record FeedbackInboxResponse(IReadOnlyList<FeedbackInboxEntry> Items);
