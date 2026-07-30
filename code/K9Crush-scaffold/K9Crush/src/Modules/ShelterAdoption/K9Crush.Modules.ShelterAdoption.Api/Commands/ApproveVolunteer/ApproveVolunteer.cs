namespace K9Crush.Modules.ShelterAdoption.Api.Commands.ApproveVolunteer;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record ApproveVolunteerResponse(Guid VolunteerApplicationId, string Status);
