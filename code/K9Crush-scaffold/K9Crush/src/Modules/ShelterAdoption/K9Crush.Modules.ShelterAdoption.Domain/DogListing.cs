using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;
using K9Crush.Modules.ShelterAdoption.Domain.Events;

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
/// A dog a shelter has listed for adoption.
///
/// Self-aggregating event-sourced entity (ADR-031, Phase 5/5). Registered
/// as its own Inline snapshot - GetDogListingDetails/GetShelterDogListings/
/// GetAdoptionListings genuinely query it. No hard delete under ES -
/// RemoveDogListingHandler appends <see cref="DogListingWithdrawnV1"/>
/// instead of session.Delete; IsRemoved flags it out of active queries.
///
/// ShelterAccountId is the FK to the listing shelter, same
/// FK-by-convention pattern as ShelterAccount.RequestedByOwnerId.
///
/// PhotoIds was ported from the removed Profiles module's DogProfile
/// (2026-07-24 descope) - the one piece of that module actually worth
/// keeping.
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
    [JsonInclude] public bool IsRemoved { get; private set; }

    /// <summary>
    /// [PLANNED -> BUILT] Spec/K9CRUSH.emlang.v3.yaml's FosteringADog
    /// chapter - who currently has this listing in foster care, if
    /// anyone. Deliberately survives PlaceInFoster -> MarkFosterDogReadyForAdoption
    /// (Status goes back to Available, but the caregiver is still fostering
    /// until EndFosterPlacement resolves it) - only EndFosterPlacement
    /// clears it.
    /// </summary>
    [JsonInclude] public Guid? CurrentFosterCaregiverOwnerId { get; private set; }

    [JsonConstructor]
    private DogListing() { }

    public static DogListing Create(DogListingAddedV1 e) => new()
    {
        ShelterAccountId = e.ShelterAccountId,
        Name = e.Name,
        Breed = e.Breed,
        AgeInMonths = e.AgeInMonths,
        Bio = e.Bio,
        AddedAt = e.AddedAt,
        Status = DogListingStatus.NotReadyYet
    };

    public void Apply(DogListingStatusUpdatedV1 e) => Status = e.Status;

    public void Apply(DogListingPlacedInFosterV1 e)
    {
        CurrentFosterCaregiverOwnerId = e.FosterCaregiverOwnerId;
        Status = DogListingStatus.InFoster;
    }

    public void Apply(FosterDogMarkedReadyForAdoptionV1 e) => Status = DogListingStatus.Available;

    public void Apply(FosterPlacementEndedV1 e)
    {
        CurrentFosterCaregiverOwnerId = null;
        if (Status != DogListingStatus.Adopted)
            Status = DogListingStatus.Available;
    }

    public void Apply(DogListingEditedV1 e)
    {
        Name = e.Name;
        Breed = e.Breed;
        AgeInMonths = e.AgeInMonths;
        Bio = e.Bio;
    }

    public void Apply(DogListingPhotoAddedV1 e)
    {
        if (!PhotoIds.Contains(e.MediaAssetId))
            PhotoIds.Add(e.MediaAssetId);
    }

    public void Apply(DogListingWithdrawnV1 e) => IsRemoved = true;

    /// <summary>
    /// v3 ENRICHMENT (Spec/K9CRUSH.emlang.v3.yaml's ShelterManagingListings
    /// chapter) - new listings start NotReadyYet, not Available (the
    /// yaml's "Add Dog Listing" event props).
    /// </summary>
    public static (DogListing DogListing, DogListingAddedV1 Event) AddNew(
        Guid shelterAccountId, string name, string breed, int ageInMonths, string bio)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        var @event = new DogListingAddedV1(
            shelterAccountId, name.Trim(), breed.Trim(), ageInMonths, bio.Trim(), DateTimeOffset.UtcNow);
        return (Create(@event), @event);
    }

    /// <summary>
    /// The emlang yaml's "Update Listing Status" -> "Listing Status
    /// Updated". State-guard (Adopted is a one-way door, only reachable
    /// via an approved Application) lives in UpdateListingStatusHandler,
    /// not here. ApproveApplicationHandler calls this method directly to
    /// reach Adopted, deliberately bypassing that handler-level guard
    /// since it's the one legitimate path.
    /// </summary>
    public DogListingStatusUpdatedV1 UpdateStatus(DogListingStatus status)
    {
        var @event = new DogListingStatusUpdatedV1(status);
        Apply(@event);
        return @event;
    }

    /// <summary>
    /// The emlang yaml's "Place Dog In Foster" -> "Dog Placed In Foster".
    /// State-guard (only valid from Available/NotReadyYet) lives in the
    /// handler.
    /// </summary>
    public DogListingPlacedInFosterV1 PlaceInFoster(Guid fosterCaregiverOwnerId)
    {
        var @event = new DogListingPlacedInFosterV1(fosterCaregiverOwnerId);
        Apply(@event);
        return @event;
    }

    /// <summary>
    /// The emlang yaml's "Mark Foster Dog Ready For Adoption" -> "Foster
    /// Dog Marked Ready For Adoption". Deliberately does NOT clear
    /// CurrentFosterCaregiverOwnerId. State-guard (only valid from
    /// InFoster) lives in the handler.
    /// </summary>
    public FosterDogMarkedReadyForAdoptionV1 MarkFosterDogReadyForAdoption()
    {
        var @event = new FosterDogMarkedReadyForAdoptionV1();
        Apply(@event);
        return @event;
    }

    /// <summary>
    /// The emlang yaml's "End Foster Placement" -> "Foster Placement
    /// Ended". Always clears CurrentFosterCaregiverOwnerId; resets Status
    /// to Available unless the listing has since become Adopted. State-
    /// guard (only valid when a placement is actually active) lives in
    /// the handler.
    /// </summary>
    public FosterPlacementEndedV1 EndFosterPlacement()
    {
        var @event = new FosterPlacementEndedV1();
        Apply(@event);
        return @event;
    }

    /// <summary>
    /// The emlang yaml's "Edit Dog Listing" -> "Dog Listing Edited". The
    /// yaml's `significantChange` prop isn't stored on this entity - it's
    /// caller-supplied per edit, not a property of the listing itself.
    /// </summary>
    public DogListingEditedV1 Edit(string name, string breed, int ageInMonths, string bio)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        var @event = new DogListingEditedV1(name.Trim(), breed.Trim(), ageInMonths, bio.Trim());
        Apply(@event);
        return @event;
    }

    /// <summary>
    /// Ported from the removed Profiles module's DogProfile.AttachPhoto -
    /// same de-duplication behavior (attaching the same MediaAssetId
    /// twice is a no-op, not an error).
    /// </summary>
    public DogListingPhotoAddedV1 AttachPhoto(Guid mediaAssetId)
    {
        var @event = new DogListingPhotoAddedV1(mediaAssetId);
        Apply(@event);
        return @event;
    }

    /// <summary>
    /// The emlang yaml's "Remove Dog Listing" -> "Dog Listing Removed" -
    /// no-hard-delete flag under ADR-031 (see this class's own comment).
    /// </summary>
    public DogListingWithdrawnV1 Remove()
    {
        var @event = new DogListingWithdrawnV1();
        Apply(@event);
        return @event;
    }
}
