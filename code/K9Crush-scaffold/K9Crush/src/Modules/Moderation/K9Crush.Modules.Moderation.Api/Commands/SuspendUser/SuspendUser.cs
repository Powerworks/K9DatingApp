namespace K9Crush.Modules.Moderation.Api.Commands.SuspendUser;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record SuspendUserResponse(Guid OwnerId, bool IsSuspended);
