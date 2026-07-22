using System.ComponentModel.DataAnnotations;

namespace K9Crush.Modules.Admin.Api.Commands.RespondToFeedback;

/// <summary>The request/command for this slice - what the caller sends.</summary>
public sealed record RespondToFeedbackRequest(
    [property: Required, MaxLength(2000)] string ResponseMessage);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record RespondToFeedbackResponse(Guid FeedbackId, string Status);
