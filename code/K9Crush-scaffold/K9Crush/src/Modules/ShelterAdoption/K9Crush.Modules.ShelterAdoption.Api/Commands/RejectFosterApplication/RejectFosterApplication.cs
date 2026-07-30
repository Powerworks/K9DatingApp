using System.ComponentModel.DataAnnotations;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.RejectFosterApplication;

/// <summary>The request/command for this slice - what the caller sends.</summary>
public sealed record RejectFosterApplicationRequest(
    [property: Required, MaxLength(1000)] string Reason);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record RejectFosterApplicationResponse(Guid FosterApplicationId, string Status);
