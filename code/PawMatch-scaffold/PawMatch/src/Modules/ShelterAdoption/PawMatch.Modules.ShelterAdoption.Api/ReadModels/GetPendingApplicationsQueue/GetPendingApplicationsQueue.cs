namespace PawMatch.Modules.ShelterAdoption.Api.ReadModels.GetPendingApplicationsQueue;

public sealed record PendingApplicationSummary(Guid ApplicationId, Guid DogListingId, Guid ApplicantOwnerId, string Status);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record PendingApplicationsQueueResponse(IReadOnlyList<PendingApplicationSummary> Items);
