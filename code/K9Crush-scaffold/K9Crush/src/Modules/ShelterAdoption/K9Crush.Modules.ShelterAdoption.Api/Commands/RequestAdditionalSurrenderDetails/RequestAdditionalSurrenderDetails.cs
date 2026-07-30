using System.ComponentModel.DataAnnotations;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.RequestAdditionalSurrenderDetails;

/// <summary>The request/command for this slice - what the caller sends.</summary>
public sealed record RequestAdditionalSurrenderDetailsRequest(
    [property: Required, MaxLength(1000)] string Reason);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record RequestAdditionalSurrenderDetailsResponse(Guid SurrenderRequestId, string Status);
