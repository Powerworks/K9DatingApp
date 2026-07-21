namespace K9Crush.Modules.Identity.Api.Commands.RequestAccountDeletion;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record RequestAccountDeletionResponse(Guid OwnerId, DateTimeOffset DeletionRequestedAt);
