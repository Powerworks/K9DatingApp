namespace K9Crush.Modules.Moderation.Api.ReadModels.GetModerationQueue;

/// <summary>One row in the queue listing - a summary, not the full detail (see GetFlaggedContentDetail for that).</summary>
public sealed record FlaggedContentEntry(
    Guid FlagId,
    string ContentType,
    Guid ContentId,
    Guid ContentOwnerId,
    Guid ReporterOwnerId,
    DateTimeOffset FlaggedAt,
    string Status);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record ModerationQueueResponse(IReadOnlyList<FlaggedContentEntry> Items);
