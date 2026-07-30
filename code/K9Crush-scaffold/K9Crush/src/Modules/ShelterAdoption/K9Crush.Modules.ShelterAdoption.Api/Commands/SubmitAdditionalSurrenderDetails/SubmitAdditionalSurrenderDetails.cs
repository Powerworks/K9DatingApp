namespace K9Crush.Modules.ShelterAdoption.Api.Commands.SubmitAdditionalSurrenderDetails;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record SubmitAdditionalSurrenderDetailsResponse(Guid SurrenderRequestId, string Status);
