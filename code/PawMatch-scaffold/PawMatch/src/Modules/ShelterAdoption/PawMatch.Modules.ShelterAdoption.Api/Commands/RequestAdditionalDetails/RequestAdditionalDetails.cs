using System.ComponentModel.DataAnnotations;

namespace PawMatch.Modules.ShelterAdoption.Api.Commands.RequestAdditionalDetails;

/// <summary>The request/command for this slice - what the caller sends.</summary>
public sealed record RequestAdditionalDetailsRequest(
    [property: Required, MaxLength(1000)] string Reason);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record RequestAdditionalDetailsResponse(Guid ApplicationId, string Status);
