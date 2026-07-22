using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.ShelterAdoption.Domain;

/// <summary>
/// v3 ENRICHMENT (Spec/K9CRUSH.emlang.v3.yaml's ShelterManagingListings
/// chapter). Available is deliberately first (ordinal 0) - see
/// DogListing.Create's comment for why that matters for pre-existing
/// documents. Order otherwise matches the yaml's own listed order.
/// </summary>
public enum DogListingStatus
{
    Available,
    NotReadyYet,
    InFoster,
    PendingAdoption,
    Adopted
}

/// <summary>
/// Current-state Marten document. A dog a shelter has listed for
/// adoption - deliberately a separate type from
/// K9Crush.Modules.Profiles.Domain.DogProfile, which is a member's own
/// dog used for the dating/swipe feature. Same real-world "a dog with a
/// name and breed" shape, genuinely different concept and lifecycle -
/// not worth collapsing into one type across two unrelated modules.
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
    [JsonInclude] public DogListingStatus Status { get; private set; }

    [JsonConstructor]
    private DogListing() { }

    /// <summary>
    /// v3 ENRICHMENT (Spec/K9CRUSH.emlang.v3.yaml's ShelterManagingListings
    /// chapter) - new listings start NotReadyYet, not Available (the
    /// yaml's "Add Dog Listing" event props). Available is deliberately
    /// enum value 0 (see DogListingStatus below), so listings created
    /// before this field existed deserialize as Available - matching
    /// their previous implicit "adoptable" meaning, no migration needed.
    /// </summary>
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
            AddedAt = DateTimeOffset.UtcNow,
            Status = DogListingStatus.NotReadyYet
        };
    }

    /// <summary>
    /// The emlang yaml's "Update Listing Status" -> "Listing Status
    /// Updated". State-guard (Adopted is a one-way door, only reachable
    /// via an approved Application) lives in UpdateListingStatusHandler,
    /// not here - same "guard lives in the handler" convention as every
    /// other status-guarded entity in this codebase (e.g. Application).
    /// ApproveApplicationHandler calls this method directly to reach
    /// Adopted, deliberately bypassing that handler-level guard since
    /// it's the one legitimate path.
    /// </summary>
    public void UpdateStatus(DogListingStatus status) => Status = status;

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
