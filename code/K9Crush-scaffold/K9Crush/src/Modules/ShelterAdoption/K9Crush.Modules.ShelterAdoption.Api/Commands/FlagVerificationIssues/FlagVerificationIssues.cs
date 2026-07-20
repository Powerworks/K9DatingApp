using System.ComponentModel.DataAnnotations;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.FlagVerificationIssues;

/// <summary>The request/command for this slice - what the caller sends.</summary>
public sealed record FlagVerificationIssuesRequest(
    [property: Required, MaxLength(1000)] string Reason);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record FlagVerificationIssuesResponse(Guid ShelterAccountId, string Status);
