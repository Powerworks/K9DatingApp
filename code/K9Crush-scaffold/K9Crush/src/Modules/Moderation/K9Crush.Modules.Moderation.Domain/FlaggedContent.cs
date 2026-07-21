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
/// Report Review) but only Media actually publishes one today - the
/// other four producer chapters don't exist as real slices yet, so
/// ContentType only has a Media member for now. Add members here (and a
/// matching projector) as each producer module actually gets built,
/// rather than speculatively now.
/// </summary>
public enum ContentType
{
    Media
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
