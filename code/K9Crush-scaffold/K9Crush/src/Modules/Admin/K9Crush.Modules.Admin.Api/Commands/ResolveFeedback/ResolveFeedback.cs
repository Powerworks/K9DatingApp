namespace K9Crush.Modules.Admin.Api.Commands.ResolveFeedback;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record ResolveFeedbackResponse(Guid FeedbackId, string Status);
