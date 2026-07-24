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
/// adoption.
///
/// ShelterAccountId is the FK to the listing shelter, same
/// FK-by-convention pattern as ShelterAccount.RequestedByOwnerId.
///
/// Follows the [JsonConstructor]/[JsonInclude] serialization pattern
/// every document-style entity in this codebase needs - Marten's default
/// System.Text.Json-based serializer only populates public constructors/
/// settable members by default; a non-public parameterless constructor
/// needs [JsonConstructor], and every non-publicly-settable property
/// needs [JsonInclude], or LoadAsync throws NotSupportedException on the
/// first real read.
///
/// Previously described as "deliberately a separate type from
/// K9Crush.Modules.Profiles.Domain.DogProfile" - that module (a member's
/// own dog used for the dating/swipe feature) was removed entirely
/// 2026-07-24 as part of the product's descope away from that framing
/// (see Spec/K9CRUSH.emlang.v3.yaml's SCOPE NOTE); PhotoIds below is the
/// one piece of DogProfile actually worth keeping, ported here rather
/// than lost with the rest of that module.
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
    [JsonInclude] public List<Guid> PhotoIds { get; private set; } = new();

    /// <summary>
    /// [PLANNED -> BUILT] Spec/K9CRUSH.emlang.v3.yaml's FosteringADog
    /// chapter - who currently has this listing in foster care, if
    /// anyone. Not a separate placement document (see this field's
    /// setters below and the chapter's own header comment) - a listing
    /// moving InFoster and back is a status change on the listing itself.
    /// Deliberately survives PlaceInFoster -> MarkFosterDogReadyForAdoption
    /// (Status goes back to Available, but the caregiver is still fostering
    /// until EndFosterPlacement resolves it) - only EndFosterPlacement
    /// clears it.
    /// </summary>
    [JsonInclude] public Guid? CurrentFosterCaregiverOwnerId { get; private set; }

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
    /// The emlang yaml's "Place Dog In Foster" -> "Dog Placed In Foster".
    /// State-guard (only valid from Available/NotReadyYet - not already
    /// InFoster, not PendingAdoption/Adopted) lives in the handler.
    /// </summary>
    public void PlaceInFoster(Guid fosterCaregiverOwnerId)
    {
        CurrentFosterCaregiverOwnerId = fosterCaregiverOwnerId;
        Status = DogListingStatus.InFoster;
    }

    /// <summary>
    /// The emlang yaml's "Mark Foster Dog Ready For Adoption" -> "Foster
    /// Dog Marked Ready For Adoption". Deliberately does NOT clear
    /// CurrentFosterCaregiverOwnerId - see that field's own comment.
    /// State-guard (only valid from InFoster) lives in the handler.
    /// </summary>
    public void MarkFosterDogReadyForAdoption() => Status = DogListingStatus.Available;

    /// <summary>
    /// The emlang yaml's "End Foster Placement" -> "Foster Placement
    /// Ended". Always clears CurrentFosterCaregiverOwnerId; resets Status
    /// to Available unless the listing has since become Adopted (that
    /// one-way door - see UpdateStatus's comment - takes precedence over
    /// closing out the foster record). State-guard (only valid when a
    /// placement is actually active) lives in the handler.
    /// </summary>
    public void EndFosterPlacement()
    {
        CurrentFosterCaregiverOwnerId = null;
        if (Status != DogListingStatus.Adopted)
            Status = DogListingStatus.Available;
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

    /// <summary>
    /// Ported from the removed Profiles module's DogProfile.AttachPhoto -
    /// same de-duplication behavior (attaching the same MediaAssetId
    /// twice is a no-op, not an error).
    /// </summary>
    public void AttachPhoto(Guid mediaAssetId)
    {
        if (!PhotoIds.Contains(mediaAssetId))
            PhotoIds.Add(mediaAssetId);
    }
}
