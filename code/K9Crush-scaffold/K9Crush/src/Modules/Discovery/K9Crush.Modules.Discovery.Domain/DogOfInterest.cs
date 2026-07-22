using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Discovery.Domain;

/// <summary>
/// Current-state Marten document (not event-sourced - this is a simple
/// bookmark fact, not swipe provenance). Records that an owner is
/// interested in a specific dog profile.
///
/// Backs TWO of TheWindowShopper chapter's named outcomes, per the
/// event-modeling call made for this chapter: "Flag Dog Of Interest" (any
/// signed-in member, any time, browsing the feed generally) and "Claim
/// Saved Match" (the guest-signup-bridging case - the client remembers a
/// dogId from before the guest signed up and passes it back once
/// Profile Confirmed) are the same underlying action from two different
/// narrative framings, not two separate mechanisms - see
/// FlagDogOfInterestHandler/ClaimSavedMatchHandler, which both call
/// Flag() below and share the same "is this dog still available" guard.
/// </summary>
public class DogOfInterest : Entity
{
    [JsonInclude] public Guid OwnerId { get; private set; }
    [JsonInclude] public Guid DogProfileId { get; private set; }
    [JsonInclude] public DateTimeOffset FlaggedAt { get; private set; }

    [JsonConstructor]
    private DogOfInterest() { }

    public static DogOfInterest Flag(Guid ownerId, Guid dogProfileId) => new()
    {
        OwnerId = ownerId,
        DogProfileId = dogProfileId,
        FlaggedAt = DateTimeOffset.UtcNow
    };
}
