namespace K9Crush.Modules.Identity.Api.ReadModels.ViewProfileSettings;

/// <summary>
/// What this slice hands back to the caller. DeletionRequestedAt/
/// GracePeriodEndsAt/IsPermanentlyDeleted surface the deletion saga's
/// current state directly rather than collapsing it into a single status
/// enum - a caller can tell "requested but not yet confirmed" (only
/// DeletionRequestedAt set) apart from "confirmed, recoverable until X"
/// (GracePeriodEndsAt also set) without this slice inventing a name for
/// every combination.
/// </summary>
public sealed record ProfileSettingsResponse(
    Guid OwnerId,
    string Email,
    string? DisplayName,
    string Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeletionRequestedAt,
    DateTimeOffset? GracePeriodEndsAt,
    bool IsPermanentlyDeleted);
