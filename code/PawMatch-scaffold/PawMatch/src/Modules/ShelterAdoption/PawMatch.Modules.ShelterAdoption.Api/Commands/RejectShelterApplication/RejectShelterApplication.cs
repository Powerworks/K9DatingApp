using System.ComponentModel.DataAnnotations;

namespace PawMatch.Modules.ShelterAdoption.Api.Commands.RejectShelterApplication;

/// <summary>The request/command for this slice - what the caller sends.</summary>
public sealed record RejectShelterApplicationRequest(
    [property: Required, MaxLength(1000)] string Reason);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record RejectShelterApplicationResponse(Guid ShelterAccountId, string Status);
