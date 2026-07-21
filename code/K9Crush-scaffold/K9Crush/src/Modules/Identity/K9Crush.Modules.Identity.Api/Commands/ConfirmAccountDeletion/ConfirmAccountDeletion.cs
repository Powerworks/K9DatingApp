namespace K9Crush.Modules.Identity.Api.Commands.ConfirmAccountDeletion;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record ConfirmAccountDeletionResponse(
    Guid OwnerId,
    DateTimeOffset GracePeriodEndsAt,
    int GracePeriodDays,
    bool Recoverable);
