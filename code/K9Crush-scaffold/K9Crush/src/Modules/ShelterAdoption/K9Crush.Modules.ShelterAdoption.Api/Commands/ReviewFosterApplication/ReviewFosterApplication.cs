namespace K9Crush.Modules.ShelterAdoption.Api.Commands.ReviewFosterApplication;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record ReviewFosterApplicationResponse(Guid FosterApplicationId, string Status);
