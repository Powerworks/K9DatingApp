namespace K9Crush.Modules.ShelterAdoption.Api.Commands.ApproveFosterCaregiver;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record ApproveFosterCaregiverResponse(Guid FosterApplicationId, string Status);
