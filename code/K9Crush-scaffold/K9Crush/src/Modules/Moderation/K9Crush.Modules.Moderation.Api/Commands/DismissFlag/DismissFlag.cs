namespace K9Crush.Modules.Moderation.Api.Commands.DismissFlag;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record DismissFlagResponse(Guid FlagId, string Status);
