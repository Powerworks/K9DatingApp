using K9Crush.Modules.Media.Domain.Events;

namespace K9Crush.Modules.Media.Api.Commands.ReportMedia;

/// <summary>
/// ADR-019 command state: the minimal, never-persisted, never-shared state
/// ReportMediaHandler needs to decide - just who owns the reported asset.
/// Computed live via session.Events.AggregateStreamAsync&lt;ReportMediaState&gt;,
/// never a LoadAsync/Query against MediaAsset itself (that would be exactly
/// the MatchAggregate-shaped mistake ADR-019 exists to prevent). No Apply
/// overload for MediaAssetSharedV1/MediaAssetRemovedV1 is needed - OwnerId
/// never changes after upload, and Marten ignores stream events with no
/// matching Apply/Create overload during aggregation.
/// </summary>
public sealed class ReportMediaState
{
    public Guid OwnerId { get; private set; }

    public static ReportMediaState Create(MediaAssetUploadedV1 e) => new() { OwnerId = e.OwnerId };
}
