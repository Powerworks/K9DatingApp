using System.Text.Json.Serialization;
using PawMatch.BuildingBlocks.Domain;

namespace PawMatch.Modules.Identity.Domain;

/// <summary>
/// Current-state Marten document, one per Supabase auth user (Identity is
/// document-centric, not event-sourced - see Solution Architecture doc
/// Section 2.1). This is a thin projection over Supabase's own user
/// lifecycle (ADR-005), not a credential store - Supabase owns
/// signup/login/password-reset entirely.
///
/// Id is deliberately set to the Supabase auth user's own id (the JWT's
/// `sub` claim), not a freshly generated Guid like Entity's default -
/// every other module's OwnerId foreign key (e.g. DogProfile.OwnerId)
/// assumes this alignment.
///
/// Follows the same [JsonConstructor]/[JsonInclude] serialization pattern
/// as DogProfile - see that file for the full writeup of why every
/// document-style entity needs it.
/// </summary>
public class OwnerAccount : Entity
{
    [JsonInclude] public string Email { get; private set; } = default!;
    [JsonInclude] public bool IsVerified { get; private set; }
    [JsonInclude] public OwnerRole Role { get; private set; }
    [JsonInclude] public DateTimeOffset CreatedAt { get; private set; }

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
    /// The only role transition currently built - triggered by
    /// ShelterAdoption's ShelterAccountCreatedV1 (see
    /// Automations/PromoteOwnerToShelterOnAccountCreated), not exposed as
    /// its own command/API. No general-purpose AssignRole endpoint exists
    /// - deliberately not built, since nothing currently needs to grant
    /// Vendor or Admin via the API; the first Admin is a manual Postgres
    /// seed (see GETTING_STARTED.md-style bootstrap note), same as most
    /// real systems' first-admin problem.
    /// </summary>
    public void PromoteToShelter() => Role = OwnerRole.Shelter;
}
