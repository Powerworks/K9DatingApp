using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Places.Domain;

/// <summary>
/// Current-state Marten document. A restaurant/dog park/groomer/trainer/
/// walker/minder/breeder listing, from the emlang yaml's
/// LeaveAReviewRestaurantOrDogPark chapter's placeType prop.
///
/// The yaml's own ClaimABusinessListing chapter is entirely about
/// CLAIMING an "existing listing" - it never shows how a listing first
/// comes into existence (no "Create Listing" command anywhere in that
/// chapter, implying listings are meant to be seeded/imported from an
/// external directory, not created via this app's own commands). Since
/// nothing else in the yaml creates one either, Commands/CreatePlaceListing
/// is a disclosed necessary gap-fill - without it, there's nothing for
/// LeaveAReviewRestaurantOrDogPark's reviews to attach to. OwnerId is set
/// to the creating caller directly, deliberately NOT wired through
/// ClaimABusinessListing's request/verify/transfer workflow - that whole
/// chapter (claim requests, contact-info verification, admin override,
/// ownership transfer) is a separate, larger, not-yet-built feature.
/// </summary>
public enum PlaceType
{
    Restaurant,
    DogPark,
    Groomer,
    Trainer,
    Walker,
    Minder,
    Breeder
}

public class Place : Entity
{
    [JsonInclude] public Guid OwnerId { get; private set; }
    [JsonInclude] public string Name { get; private set; } = default!;
    [JsonInclude] public PlaceType PlaceType { get; private set; }
    [JsonInclude] public DateTimeOffset CreatedAt { get; private set; }

    [JsonConstructor]
    private Place() { }

    public static Place Create(Guid ownerId, string name, PlaceType placeType)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        return new Place
        {
            OwnerId = ownerId,
            Name = name.Trim(),
            PlaceType = placeType,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
