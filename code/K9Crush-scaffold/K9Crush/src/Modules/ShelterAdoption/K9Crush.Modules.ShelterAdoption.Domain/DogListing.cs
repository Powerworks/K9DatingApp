using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.ShelterAdoption.Domain;

/// <summary>
/// Current-state Marten document. A dog a shelter has listed for
/// adoption - deliberately a separate type from
/// K9Crush.Modules.Profiles.Domain.DogProfile, which is a member's own
/// dog used for the dating/swipe feature. Same real-world "a dog with a
/// name and breed" shape, genuinely different concept and lifecycle -
/// not worth collapsing into one type across two unrelated modules.
///
/// Still no Status field (listed/pending_applications/adopted, per the
/// ShelterManagingListings chapter's read model) - "pending_applications"/
/// "adopted" need an Application entity that doesn't exist yet
/// (TheWouldBeAdopter/ShelterReviewsApplication chapters), and RemoveDogListing
/// turned out not to need one either (see that handler - it's a genuine
/// document delete, "removed" isn't even one of this enum's own listed
/// values). Added if and when a slice actually needs to distinguish them.
///
/// ShelterAccountId is the FK to the listing shelter, same
/// FK-by-convention pattern as ShelterAccount.RequestedByOwnerId.
///
/// Follows the same [JsonConstructor]/[JsonInclude] serialization pattern
/// as every other document-style entity - see DogProfile.cs for the full
/// writeup of why.
/// </summary>
public class DogListing : Entity
{
    [JsonInclude] public Guid ShelterAccountId { get; private set; }
    [JsonInclude] public string Name { get; private set; } = default!;
    [JsonInclude] public string Breed { get; private set; } = default!;
    [JsonInclude] public int AgeInMonths { get; private set; }
    [JsonInclude] public string Bio { get; private set; } = string.Empty;
    [JsonInclude] public DateTimeOffset AddedAt { get; private set; }

    [JsonConstructor]
    private DogListing() { }

    public static DogListing Create(Guid shelterAccountId, string name, string breed, int ageInMonths, string bio)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        return new DogListing
        {
            ShelterAccountId = shelterAccountId,
            Name = name.Trim(),
            Breed = breed.Trim(),
            AgeInMonths = ageInMonths,
            Bio = bio.Trim(),
            AddedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// The emlang yaml's "Edit Dog Listing" -> "Dog Listing Edited". The
    /// yaml's `significantChange` prop isn't stored on this document -
    /// it's caller-supplied per edit (see EditDogListingRequest), not a
    /// property of the listing itself, and only matters as the guard on
    /// whether EditDogListingHandler cascades DogListingSignificantlyEditedV1
    /// (ADR-028) - nothing reads it back later.
    /// </summary>
    public void Edit(string name, string breed, int ageInMonths, string bio)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        Name = name.Trim();
        Breed = breed.Trim();
        AgeInMonths = ageInMonths;
        Bio = bio.Trim();
    }
}
