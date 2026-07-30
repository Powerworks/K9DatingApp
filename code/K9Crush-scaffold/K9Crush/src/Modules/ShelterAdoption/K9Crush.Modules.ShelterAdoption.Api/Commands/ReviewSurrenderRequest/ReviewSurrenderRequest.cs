namespace K9Crush.Modules.ShelterAdoption.Api.Commands.ReviewSurrenderRequest;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record ReviewSurrenderRequestResponse(Guid SurrenderRequestId, string Status);
