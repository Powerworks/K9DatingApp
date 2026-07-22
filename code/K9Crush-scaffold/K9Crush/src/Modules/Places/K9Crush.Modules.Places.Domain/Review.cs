using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Places.Domain;

/// <summary>
/// Current-state Marten document. The emlang yaml's
/// LeaveAReviewRestaurantOrDogPark chapter: Write -> Publish -> (Edit |
/// Remove | Respond | Report). Body is a disclosed necessary field - the
/// yaml only shows a `rating` prop, but a rating with no review text
/// isn't a real review; same "gap-fill a field the yaml's props didn't
/// list" precedent as AddDogProfileDetails' Location and
/// NotificationTemplate's Subject/Body.
/// </summary>
public enum ReviewStatus
{
    Draft,
    Published,
    Removed
}

public enum ResponderRole
{
    BusinessOwner,
    ParkOwner,
    DogWalker,
    DogTrainer
}

public class Review : Entity
{
    [JsonInclude] public Guid PlaceId { get; private set; }
    [JsonInclude] public Guid ReviewerOwnerId { get; private set; }
    [JsonInclude] public int Rating { get; private set; }
    [JsonInclude] public string Body { get; private set; } = default!;

    /// <summary>
    /// The yaml's own prop on "Write Review" - accepted but unused, same
    /// "no infra to actually verify a visit exists" disclosed no-op as
    /// Media's RemoveMediaRequest.CascadeDeletesEngagement.
    /// </summary>
    [JsonInclude] public bool VisitVerificationRequired { get; private set; }

    [JsonInclude] public ReviewStatus Status { get; private set; }
    [JsonInclude] public string? ResponseText { get; private set; }
    [JsonInclude] public ResponderRole? ResponderRole { get; private set; }
    [JsonInclude] public DateTimeOffset? RespondedAt { get; private set; }

    [JsonConstructor]
    private Review() { }

    public static Review Write(Guid placeId, Guid reviewerOwnerId, int rating, string body, bool visitVerificationRequired)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required.", nameof(body));

        return new Review
        {
            PlaceId = placeId,
            ReviewerOwnerId = reviewerOwnerId,
            Rating = rating,
            Body = body.Trim(),
            VisitVerificationRequired = visitVerificationRequired,
            Status = ReviewStatus.Draft
        };
    }

    /// <summary>The emlang yaml's "Publish Review" -> "Review Published". State-guard (only valid from Draft) lives in the handler.</summary>
    public void Publish() => Status = ReviewStatus.Published;

    /// <summary>The emlang yaml's "Edit Review" -> "Review Edited". State-guard (only valid from Published) lives in the handler.</summary>
    public void Edit(int rating, string body)
    {
        Rating = rating;
        Body = body.Trim();
    }

    /// <summary>
    /// The emlang yaml's "Remove Review" -> "Review Removed" - a soft
    /// delete (Status flag), not a hard document delete, since a removed
    /// review could still have a business response or a moderation
    /// report attached that reference it. State-guard lives in the handler.
    /// </summary>
    public void Remove() => Status = ReviewStatus.Removed;

    /// <summary>The emlang yaml's "Respond To Review" -> "Review Response Posted". State-guard (only valid from Published) lives in the handler.</summary>
    public void Respond(string responseText, ResponderRole responderRole)
    {
        ResponseText = responseText.Trim();
        ResponderRole = responderRole;
        RespondedAt = DateTimeOffset.UtcNow;
    }
}
