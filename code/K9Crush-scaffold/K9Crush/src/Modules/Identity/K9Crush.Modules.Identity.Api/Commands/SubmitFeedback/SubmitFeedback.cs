using System.ComponentModel.DataAnnotations;

namespace K9Crush.Modules.Identity.Api.Commands.SubmitFeedback;

/// <summary>The request/command for this slice - what the caller sends.</summary>
public sealed record SubmitFeedbackRequest(
    [property: Required, MaxLength(2000)] string Message);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record SubmitFeedbackResponse(Guid FeedbackId);
