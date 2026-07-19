namespace PawMatch.Modules.Identity.Api.ReadModels.OwnerAccountView;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record OwnerAccountResponse(
    Guid OwnerId,
    string Email,
    bool IsVerified,
    DateTimeOffset CreatedAt);
