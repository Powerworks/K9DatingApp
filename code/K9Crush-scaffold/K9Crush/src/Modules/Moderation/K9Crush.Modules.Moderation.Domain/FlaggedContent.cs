using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Moderation.Domain;

/// <summary>
/// Current-state Marten document. The emlang yaml's
/// ModeratingFlaggedContentUserReports chapter's moderation-queue entry -
/// built from whichever producer module's own "Content Flagged" event
/// fired (see Api/ReadModels/Projectors). "Content Flagged" is shared
/// across five yaml chapters (Media's Report Media, Chat's Report
/// Message, ActivityFeed's Report Post, LeaveAReviewRestaurantOrDogPark's
/// Report Review) - Media and Places (reviews) are the two real
/// producers so far; Chat/ActivityFeed's two remaining chapters don't
/// exist as real slices yet. Add members here (and a matching projector)
/// as each producer module actually gets built, rather than
/// speculatively now.
///
/// Marten/System.Text.Json serializes this enum as its integer ordinal
/// (confirmed via OwnerRole in the Identity module) - new members are
/// always appended at the end, never inserted, so an already-persisted
/// FlaggedContent's meaning never silently changes.
/// </summary>
public enum ContentType
{
    Media,
    Review
}

public enum FlaggedContentStatus
{
    Open,
    Dismissed,
    ContentRemoved
}

public class FlaggedContent : Entity
{
    [JsonInclude] public ContentType ContentType { get; private set; }
    [JsonInclude] public Guid ContentId { get; private set; }
    [JsonInclude] public Guid ContentOwnerId { get; private set; }
    [JsonInclude] public Guid ReporterOwnerId { get; private set; }
    [JsonInclude] public DateTimeOffset FlaggedAt { get; private set; }
    [JsonInclude] public FlaggedContentStatus Status { get; private set; }

    [JsonConstructor]
    private FlaggedContent() { }

    public static FlaggedContent Create(ContentType contentType, Guid contentId, Guid contentOwnerId, Guid reporterOwnerId, DateTimeOffset flaggedAt)
    {
        return new FlaggedContent
        {
            ContentType = contentType,
            ContentId = contentId,
            ContentOwnerId = contentOwnerId,
            ReporterOwnerId = reporterOwnerId,
            FlaggedAt = flaggedAt,
            Status = FlaggedContentStatus.Open
        };
    }

    /// <summary>The emlang yaml's "Dismiss Flag" -> "Flag Dismissed" - no action needed. State-guard lives in the handler.</summary>
    public void Dismiss() => Status = FlaggedContentStatus.Dismissed;

    /// <summary>The emlang yaml's "Remove Content" -> "Content Removed". State-guard lives in the handler.</summary>
    public void MarkContentRemoved() => Status = FlaggedContentStatus.ContentRemoved;
}
