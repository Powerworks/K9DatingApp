namespace K9Crush.Modules.Moderation.Api.Commands.RemoveContent;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record RemoveContentResponse(Guid FlagId, string Status);
