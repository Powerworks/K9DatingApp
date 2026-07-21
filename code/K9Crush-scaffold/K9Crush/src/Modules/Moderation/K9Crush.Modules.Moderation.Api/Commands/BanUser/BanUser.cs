namespace K9Crush.Modules.Moderation.Api.Commands.BanUser;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record BanUserResponse(Guid OwnerId, bool IsBanned);
