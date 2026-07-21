using System.ComponentModel.DataAnnotations;
using K9Crush.Modules.Places.Domain;

namespace K9Crush.Modules.Places.Api.Commands.RespondToReview;

/// <summary>The request/command for this slice - what the caller sends.</summary>
public sealed record RespondToReviewRequest(
    [property: Required, MaxLength(2000)] string ResponseText,
    ResponderRole ResponderRole);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record RespondToReviewResponse(Guid ReviewId, string ResponseText, string ResponderRole);
