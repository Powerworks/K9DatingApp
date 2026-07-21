namespace K9Crush.Modules.Moderation.Api.Commands.WarnUser;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record WarnUserResponse(Guid OwnerId, int WarningCount);
