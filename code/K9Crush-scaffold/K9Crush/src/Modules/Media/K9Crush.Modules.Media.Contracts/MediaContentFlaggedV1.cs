using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Media.Contracts;

/// <summary>
/// Published by Commands/ReportMedia - the emlang yaml's
/// UploadShareRemovePhotosAndVideos chapter's "Report Media" ->
/// "Content Flagged". Previously consumed by the Moderation module (cut
/// 2026-07-23 as part of the product's descope away from social/dating
/// features - see Spec/K9CRUSH.emlang.v3.yaml's SCOPE NOTE); this event
/// currently has no consumer. Left in place since "report inappropriate
/// media" is a reasonable standalone feature independent of Moderation's
/// removal, not something the descope decision explicitly cut - disclosed
/// gap, not silently dropped.
/// </summary>
public sealed record MediaContentFlaggedV1(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid MediaAssetId,
    Guid ContentOwnerId,
    Guid ReporterOwnerId) : IIntegrationEvent;
