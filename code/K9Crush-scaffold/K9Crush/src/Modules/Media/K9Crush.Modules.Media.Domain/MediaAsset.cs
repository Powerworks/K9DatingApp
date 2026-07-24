using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;
using K9Crush.Modules.Media.Domain.Events;

namespace K9Crush.Modules.Media.Domain;

/// <summary>
/// The emlang yaml's UploadShareRemovePhotosAndVideos chapter's uploaded
/// photo/video - the first real entity backing what was previously just an
/// opaque `Guid MediaAssetId` accepted by Profiles.Api's
/// AddDogProfilePhotoHandler (that module has since been removed/merged
/// into ShelterAdoption; DogListing.AttachPhoto is the current caller).
///
/// StorageUrl is caller-supplied, not computed here - per ADR-005/024,
/// Supabase Storage is externally managed and this backend never handles
/// raw file bytes; the client uploads directly to Supabase Storage and
/// only tells us the resulting object reference.
///
/// Self-aggregating event-sourced entity (ADR-031, Phase 1/5 - the
/// proof-of-concept module for this retrofit): Create/Apply overloads are
/// what Marten replays via session.Events.FetchForWriting&lt;MediaAsset&gt;()
/// (write side, used by every command handler below) and
/// session.Events.AggregateStreamAsync&lt;MediaAsset&gt;() (a live read, if
/// ever needed) - no Inline snapshot is registered for this entity, since
/// no ReadModels/** slice queries it today; add one in MediaModule.cs if
/// that changes, following the dual-use pattern documented in ADR-031.
/// Still derives from Entity and keeps [JsonConstructor]/[JsonInclude] for
/// consistency with every other entity in the codebase and in case a
/// snapshot registration is added later - see docs/05-event-modeling-blueprint.md
/// Section 6.1.
/// </summary>
public enum MediaType
{
    Photo,
    Video
}

public enum MediaVisibility
{
    Public,
    FollowersOnly,
    SpecificPeople
}

public class MediaAsset : Entity
{
    [JsonInclude] public Guid OwnerId { get; private set; }
    [JsonInclude] public MediaType MediaType { get; private set; }
    [JsonInclude] public string StorageUrl { get; private set; } = default!;
    [JsonInclude] public DateTimeOffset UploadedAt { get; private set; }
    [JsonInclude] public MediaVisibility? Visibility { get; private set; }
    [JsonInclude] public IReadOnlyList<Guid> SharedWithOwnerIds { get; private set; } = [];
    [JsonInclude] public DateTimeOffset? SharedAt { get; private set; }
    [JsonInclude] public bool IsRemoved { get; private set; }

    [JsonConstructor]
    private MediaAsset() { }

    public static MediaAsset Create(MediaAssetUploadedV1 e) => new()
    {
        Id = e.MediaAssetId,
        OwnerId = e.OwnerId,
        MediaType = e.MediaType,
        StorageUrl = e.StorageUrl,
        UploadedAt = e.OccurredAt
    };

    public void Apply(MediaAssetSharedV1 e)
    {
        Visibility = e.Visibility;
        SharedWithOwnerIds = e.SharedWithOwnerIds;
        SharedAt = e.OccurredAt;
    }

    public void Apply(MediaAssetRemovedV1 e) => IsRemoved = true;

    public static (MediaAsset MediaAsset, MediaAssetUploadedV1 Event) Upload(Guid ownerId, MediaType mediaType, string storageUrl)
    {
        var @event = new MediaAssetUploadedV1(Guid.NewGuid(), ownerId, mediaType, storageUrl, DateTimeOffset.UtcNow);
        return (Create(@event), @event);
    }

    /// <summary>
    /// The emlang yaml's "Share Media" -> "Media Shared". sharedWithOwnerIds
    /// only means anything when visibility is SpecificPeople - the handler
    /// is responsible for that cross-field rule (IValidatableObject on the
    /// request), this method just records whatever it's given. State-guard
    /// (must already be uploaded, which is trivially true for any fetched
    /// MediaAsset) lives in the handler per this codebase's convention.
    /// </summary>
    public MediaAssetSharedV1 Share(MediaVisibility visibility, IReadOnlyList<Guid> sharedWithOwnerIds)
    {
        var @event = new MediaAssetSharedV1(visibility, sharedWithOwnerIds, DateTimeOffset.UtcNow);
        Apply(@event);
        return @event;
    }

    /// <summary>
    /// The emlang yaml's "Remove Media" -> "Media Removed". A flag, not a
    /// stream delete - see MediaAssetRemovedV1's own doc comment.
    /// </summary>
    public MediaAssetRemovedV1 Remove()
    {
        var @event = new MediaAssetRemovedV1(DateTimeOffset.UtcNow);
        Apply(@event);
        return @event;
    }
}
