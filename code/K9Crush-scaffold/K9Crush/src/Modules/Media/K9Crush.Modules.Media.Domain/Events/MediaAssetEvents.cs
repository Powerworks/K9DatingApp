namespace K9Crush.Modules.Media.Domain.Events;

/// <summary>
/// ADR-031 event-sourcing retrofit, Phase 1/5 (the proof-of-concept
/// module). One record per MediaAsset transition, matching the entity's own
/// domain methods 1:1. Deliberately separate from the Contracts project's
/// integration events (e.g. MediaContentFlaggedV1) even where a moment is
/// "the same" conceptually - Contracts is this module's public API surface,
/// not its storage schema.
/// </summary>
public sealed record MediaAssetUploadedV1(Guid MediaAssetId, Guid OwnerId, MediaType MediaType, string StorageUrl, DateTimeOffset OccurredAt);

public sealed record MediaAssetSharedV1(MediaVisibility Visibility, IReadOnlyList<Guid> SharedWithOwnerIds, DateTimeOffset OccurredAt);

/// <summary>
/// Flag, not a stream delete - RemoveMediaHandler used to do a genuine
/// session.Delete(mediaAsset); event streams don't support that, so removal
/// becomes an in-stream fact (IsRemoved) instead. Same pattern this
/// retrofit will apply to DogListingRemovedV1 in Phase 5.
/// </summary>
public sealed record MediaAssetRemovedV1(DateTimeOffset OccurredAt);
