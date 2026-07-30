namespace K9Crush.Modules.ShelterAdoption.Api.ReadModels.GetVolunteerApplicationsQueue;

public sealed record VolunteerApplicationSummary(
    Guid VolunteerApplicationId, Guid ApplicantOwnerId, string AreaOfInterest, string Status);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record VolunteerApplicationsQueueResponse(IReadOnlyList<VolunteerApplicationSummary> Items);
