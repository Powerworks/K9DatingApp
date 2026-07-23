namespace K9Crush.Modules.ShelterAdoption.Api.Commands.ReviewVolunteerApplication;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record ReviewVolunteerApplicationResponse(Guid VolunteerApplicationId, string Status);
