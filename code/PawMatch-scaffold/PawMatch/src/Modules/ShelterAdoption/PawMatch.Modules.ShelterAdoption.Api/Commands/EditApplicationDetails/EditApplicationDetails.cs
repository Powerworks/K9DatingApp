using System.ComponentModel.DataAnnotations;

namespace PawMatch.Modules.ShelterAdoption.Api.Commands.EditApplicationDetails;

/// <summary>The request/command for this slice - what the caller sends.</summary>
public sealed record EditApplicationDetailsRequest(
    [property: Required, MaxLength(2000)] string Details);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record EditApplicationDetailsResponse(Guid ApplicationId, DateTimeOffset LastEditedAt);
