namespace K9Crush.Modules.ShelterAdoption.Api.ReadModels.GetFosterApplicationsQueue;

public sealed record FosterApplicationSummary(Guid FosterApplicationId, Guid ApplicantOwnerId, string Status);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record FosterApplicationsQueueResponse(IReadOnlyList<FosterApplicationSummary> Items);
