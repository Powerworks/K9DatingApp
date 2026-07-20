using System.ComponentModel.DataAnnotations;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.RejectApplication;

/// <summary>The request/command for this slice - what the caller sends.</summary>
public sealed record RejectApplicationRequest(
    [property: Required, MaxLength(1000)] string Reason);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record RejectApplicationResponse(Guid ApplicationId, string Status);
