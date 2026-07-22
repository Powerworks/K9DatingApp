namespace K9Crush.Modules.Moderation.Api.ReadModels.GetFlaggedContentDetail;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record FlaggedContentDetailResponse(
    Guid FlagId,
    string ContentType,
    Guid ContentId,
    Guid ContentOwnerId,
    Guid ReporterOwnerId,
    DateTimeOffset FlaggedAt,
    string Status);
