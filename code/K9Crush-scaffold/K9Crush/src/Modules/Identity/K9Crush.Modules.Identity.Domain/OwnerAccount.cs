using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Identity.Domain;

/// <summary>
/// Current-state Marten document, one per Supabase auth user (Identity is
/// document-centric, not event-sourced - see Solution Architecture doc
/// Section 2.1). This is a thin projection over Supabase's own user
/// lifecycle (ADR-005), not a credential store - Supabase owns
/// signup/login/password-reset entirely.
///
/// Id is deliberately set to the Supabase auth user's own id (the JWT's
/// `sub` claim), not a freshly generated Guid like Entity's default -
/// every other module's OwnerId foreign key (e.g. DogListing.ShelterAccountId)
/// assumes this alignment.
///
/// Follows the same [JsonConstructor]/[JsonInclude] serialization pattern
/// as every document-style entity in this codebase - see
/// docs/05-event-modeling-blueprint.md Section 6.1 for the full writeup of
/// why every document-style entity needs it.
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

    public static OwnerAccount Create(Guid supabaseUserId, string email, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        return new OwnerAccount
        {
            Id = supabaseUserId,
            Email = email.Trim(),
            IsVerified = false,
            Role = OwnerRole.Owner,
            CreatedAt = createdAt
        };
    }

    public void MarkVerified() => IsVerified = true;

    /// <summary>
    /// Triggered by ShelterAdoption's ShelterAccountCreatedV1 (see
    /// Automations/PromoteOwnerToShelterOnAccountCreated), not exposed as
    /// its own command/API.
    /// </summary>
    public void PromoteToShelter() => Role = OwnerRole.Shelter;

    /// <summary>
    /// The first-admin bootstrap (see Commands/BootstrapAdmin) - the
    /// "zero admins exist yet" guard lives in the handler, not here, same
    /// as every other state-guard in this codebase. No general-purpose
    /// AssignRole endpoint exists beyond this and PromoteToShelter -
    /// deliberately not built, since nothing currently needs to grant
    /// Vendor via the API.
    /// </summary>
    public void PromoteToAdmin() => Role = OwnerRole.Admin;

    /// <summary>The emlang yaml's "Update Profile Details" -> "Profile Details Updated". State-guard (not permanently deleted) lives in the handler.</summary>
    public void UpdateDisplayName(string displayName) => DisplayName = displayName.Trim();

    /// <summary>The emlang yaml's "Request Account Deletion" -> "Account Deletion Requested". State-guard (not already pending/deleted) lives in the handler.</summary>
    public void RequestDeletion() => DeletionRequestedAt = DateTimeOffset.UtcNow;

    /// <summary>
    /// The emlang yaml's "Confirm Account Deletion" -> "Account Deleted"
    /// (gracePeriodDays: 30, recoverable: true). Setting GracePeriodEndsAt
    /// (not DeletionRequestedAt) is what actually starts the recoverable
    /// grace period - the handler schedules the matching ADR-026 check
    /// message for gracePeriodDays out. State-guard (deletion was
    /// requested, not already confirmed) lives in the handler.
    /// </summary>
    public void ConfirmDeletion(int gracePeriodDays) => GracePeriodEndsAt = DateTimeOffset.UtcNow.AddDays(gracePeriodDays);

    /// <summary>The emlang yaml's "Log In During Grace Period" -> "Account Recovered". State-guard (grace period still open) lives in the handler.</summary>
    public void RecoverAccount()
    {
        DeletionRequestedAt = null;
        GracePeriodEndsAt = null;
    }

    /// <summary>
    /// The emlang yaml's "Permanently Delete Account After Grace Period"
    /// -> "Account Permanently Deleted" (ADR-026 scheduled message,
    /// re-checked live in Automations/PermanentlyDeleteAccountAfterGracePeriod
    /// before calling this - same re-check discipline as
    /// MarkApplicationStaleHandler). Deliberately does not purge Email/
    /// DisplayName/other fields or hard-delete the document - Supabase
    /// (ADR-005) owns the actual auth user lifecycle, this flag is only
    /// this module's own record that the account is terminally gone.
    /// </summary>
    public void PermanentlyDelete() => IsPermanentlyDeleted = true;
}
