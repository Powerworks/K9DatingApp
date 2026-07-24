using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;
using K9Crush.Modules.Identity.Domain.Events;

namespace K9Crush.Modules.Identity.Domain;

/// <summary>
/// One per Supabase auth user. This is a thin projection over Supabase's
/// own user lifecycle (ADR-005), not a credential store - Supabase owns
/// signup/login/password-reset entirely.
///
/// Id is deliberately set to the Supabase auth user's own id (the JWT's
/// `sub` claim), not a freshly generated Guid - every other module's
/// OwnerId foreign key (e.g. DogListing.ShelterAccountId) assumes this
/// alignment. Under ADR-031 this makes OwnerAccount's stream id
/// externally supplied rather than freshly generated here - same wrinkle
/// as Admin's FeedbackInboxItem (Phase 2), just the second occurrence.
///
/// Self-aggregating event-sourced entity (ADR-031, Phase 4/5). Registered
/// as its own Inline snapshot in IdentityModule.cs: OwnerAccountViewHandler/
/// ViewProfileSettingsHandler genuinely query it, and MartenOwnerRoleLookup
/// (ADR-017's role lookup, read on every Admin/Shelter-policy-gated
/// request) does a plain LoadAsync against it too - not an ADR-019
/// violation, since that lookup never mutates/decides OwnerAccount's own
/// validity, only reads its current Role for an unrelated authorization
/// decision (same shape as NotificationDispatcher reading
/// NotificationPreference, Phase 3).
/// </summary>
public class OwnerAccount : Entity
{
    [JsonInclude] public string Email { get; private set; } = default!;
    [JsonInclude] public bool IsVerified { get; private set; }
    [JsonInclude] public OwnerRole Role { get; private set; }
    [JsonInclude] public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// AccountProfileSettings' "Update Profile Details" - the yaml lists
    /// no props for this command, so this is a disclosed judgment call:
    /// the only human-facing "profile detail" that plausibly belongs on
    /// the owner's own account rather than a dog's (K9Crush.Modules.
    /// ShelterAdoption.Domain.DogListing owns everything dog-related). Null
    /// until the owner sets one.
    /// </summary>
    [JsonInclude] public string? DisplayName { get; private set; }

    /// <summary>
    /// AccountProfileSettings' deletion saga (see Commands/RequestAccountDeletion,
    /// ConfirmAccountDeletion, RecoverAccount, Automations/
    /// PermanentlyDeleteAccountAfterGracePeriod). Non-null from Request
    /// through to either RecoverAccount (cleared) or permanent deletion
    /// (left set, historical). GracePeriodEndsAt is null until
    /// ConfirmAccountDeletion; its presence (not DeletionRequestedAt's)
    /// is what actually starts the 30-day clock.
    /// </summary>
    [JsonInclude] public DateTimeOffset? DeletionRequestedAt { get; private set; }
    [JsonInclude] public DateTimeOffset? GracePeriodEndsAt { get; private set; }
    [JsonInclude] public bool IsPermanentlyDeleted { get; private set; }

    [JsonConstructor]
    private OwnerAccount() { }

    public static OwnerAccount Create(OwnerAccountCreatedV1 e) => new()
    {
        Id = e.SupabaseUserId,
        Email = e.Email.Trim(),
        IsVerified = false,
        Role = OwnerRole.Owner,
        CreatedAt = e.CreatedAt
    };

    public static (OwnerAccount OwnerAccount, OwnerAccountCreatedV1 Event) CreateNew(Guid supabaseUserId, string email, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        var @event = new OwnerAccountCreatedV1(supabaseUserId, email, createdAt);
        return (Create(@event), @event);
    }

    public void Apply(OwnerAccountVerifiedV1 e) => IsVerified = true;
    public void Apply(OwnerAccountPromotedToShelterV1 e) => Role = OwnerRole.Shelter;
    public void Apply(OwnerAccountPromotedToAdminV1 e) => Role = OwnerRole.Admin;
    public void Apply(OwnerAccountDisplayNameUpdatedV1 e) => DisplayName = e.DisplayName;
    public void Apply(OwnerAccountDeletionRequestedV1 e) => DeletionRequestedAt = e.RequestedAt;
    public void Apply(OwnerAccountDeletionConfirmedV1 e) => GracePeriodEndsAt = e.GracePeriodEndsAt;

    public void Apply(OwnerAccountRecoveredV1 e)
    {
        DeletionRequestedAt = null;
        GracePeriodEndsAt = null;
    }

    public void Apply(OwnerAccountPermanentlyDeletedV1 e) => IsPermanentlyDeleted = true;

    public OwnerAccountVerifiedV1 MarkVerified()
    {
        var @event = new OwnerAccountVerifiedV1();
        Apply(@event);
        return @event;
    }

    /// <summary>
    /// Triggered by ShelterAdoption's ShelterAccountCreatedV1 (see
    /// Automations/PromoteOwnerToShelterOnAccountCreated), not exposed as
    /// its own command/API.
    /// </summary>
    public OwnerAccountPromotedToShelterV1 PromoteToShelter()
    {
        var @event = new OwnerAccountPromotedToShelterV1();
        Apply(@event);
        return @event;
    }

    /// <summary>
    /// The first-admin bootstrap (see Commands/BootstrapAdmin) - the
    /// "zero admins exist yet" guard lives in the handler, not here, same
    /// as every other state-guard in this codebase. No general-purpose
    /// AssignRole endpoint exists beyond this and PromoteToShelter -
    /// deliberately not built, since nothing currently needs to grant
    /// Vendor via the API.
    /// </summary>
    public OwnerAccountPromotedToAdminV1 PromoteToAdmin()
    {
        var @event = new OwnerAccountPromotedToAdminV1();
        Apply(@event);
        return @event;
    }

    /// <summary>The emlang yaml's "Update Profile Details" -> "Profile Details Updated". State-guard (not permanently deleted) lives in the handler.</summary>
    public OwnerAccountDisplayNameUpdatedV1 UpdateDisplayName(string displayName)
    {
        var @event = new OwnerAccountDisplayNameUpdatedV1(displayName.Trim());
        Apply(@event);
        return @event;
    }

    /// <summary>The emlang yaml's "Request Account Deletion" -> "Account Deletion Requested". State-guard (not already pending/deleted) lives in the handler.</summary>
    public OwnerAccountDeletionRequestedV1 RequestDeletion()
    {
        var @event = new OwnerAccountDeletionRequestedV1(DateTimeOffset.UtcNow);
        Apply(@event);
        return @event;
    }

    /// <summary>
    /// The emlang yaml's "Confirm Account Deletion" -> "Account Deleted"
    /// (gracePeriodDays: 30, recoverable: true). Setting GracePeriodEndsAt
    /// (not DeletionRequestedAt) is what actually starts the recoverable
    /// grace period - the handler schedules the matching ADR-026 check
    /// message for gracePeriodDays out. State-guard (deletion was
    /// requested, not already confirmed) lives in the handler.
    /// </summary>
    public OwnerAccountDeletionConfirmedV1 ConfirmDeletion(int gracePeriodDays)
    {
        var @event = new OwnerAccountDeletionConfirmedV1(DateTimeOffset.UtcNow.AddDays(gracePeriodDays));
        Apply(@event);
        return @event;
    }

    /// <summary>The emlang yaml's "Log In During Grace Period" -> "Account Recovered". State-guard (grace period still open) lives in the handler.</summary>
    public OwnerAccountRecoveredV1 RecoverAccount()
    {
        var @event = new OwnerAccountRecoveredV1();
        Apply(@event);
        return @event;
    }

    /// <summary>
    /// The emlang yaml's "Permanently Delete Account After Grace Period"
    /// -> "Account Permanently Deleted" (ADR-026 scheduled message,
    /// re-checked live in Automations/PermanentlyDeleteAccountAfterGracePeriod
    /// before calling this - same re-check discipline as
    /// MarkApplicationStaleHandler). Deliberately does not purge Email/
    /// DisplayName/other fields or hard-delete the stream - Supabase
    /// (ADR-005) owns the actual auth user lifecycle, this flag is only
    /// this module's own record that the account is terminally gone.
    /// </summary>
    public OwnerAccountPermanentlyDeletedV1 PermanentlyDelete()
    {
        var @event = new OwnerAccountPermanentlyDeletedV1();
        Apply(@event);
        return @event;
    }
}
