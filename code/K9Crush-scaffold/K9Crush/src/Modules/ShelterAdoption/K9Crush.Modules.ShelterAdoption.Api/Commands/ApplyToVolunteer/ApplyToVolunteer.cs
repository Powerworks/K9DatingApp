using K9Crush.Modules.ShelterAdoption.Domain;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.ApplyToVolunteer;

/// <summary>The request/command for this slice - what the caller sends.</summary>
public sealed record ApplyToVolunteerRequest(VolunteerAreaOfInterest AreaOfInterest);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record ApplyToVolunteerResponse(Guid VolunteerApplicationId);
