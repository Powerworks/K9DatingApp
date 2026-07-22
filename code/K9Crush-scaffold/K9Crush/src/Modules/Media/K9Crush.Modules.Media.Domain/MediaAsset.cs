using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Media.Domain;

/// <summary>
/// Current-state Marten document. The emlang yaml's
/// UploadShareRemovePhotosAndVideos chapter's uploaded photo/video - this
/// is the first real entity backing what was previously just an opaque
/// `Guid MediaAssetId` accepted by Profiles.Api's AddDogProfilePhotoHandler
/// (that slice was built before this module existed; a caller is expected
/// to call this module's UploadMedia first and pass the resulting Id into
/// AddDogProfilePhoto, same as any other cross-module reference by id in
/// this codebase).
///
/// StorageUrl is caller-supplied, not computed here - per ADR-005/024,
/// Supabase Storage is externally managed and this backend never handles
/// raw file bytes; the client uploads directly to Supabase Storage and
/// only tells us the resulting object reference.
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

    [JsonConstructor]
    private MediaAsset() { }

    public static MediaAsset Upload(Guid ownerId, MediaType mediaType, string storageUrl)
    {
        return new MediaAsset
        {
            OwnerId = ownerId,
            MediaType = mediaType,
            StorageUrl = storageUrl,
            UploadedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// The emlang yaml's "Share Media" -> "Media Shared". sharedWithOwnerIds
    /// only means anything when visibility is SpecificPeople - the handler
    /// is responsible for that cross-field rule (IValidatableObject on the
    /// request), this method just records whatever it's given. State-guard
    /// (must already be uploaded, which is trivially true for any loaded
    /// MediaAsset) lives in the handler per this codebase's convention.
    /// </summary>
    public void Share(MediaVisibility visibility, IReadOnlyList<Guid> sharedWithOwnerIds)
    {
        Visibility = visibility;
        SharedWithOwnerIds = sharedWithOwnerIds;
        SharedAt = DateTimeOffset.UtcNow;
    }
}
