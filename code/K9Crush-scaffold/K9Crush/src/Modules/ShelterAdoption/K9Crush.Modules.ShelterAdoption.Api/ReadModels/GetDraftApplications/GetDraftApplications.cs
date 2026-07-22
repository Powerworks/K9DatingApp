namespace K9Crush.Modules.ShelterAdoption.Api.ReadModels.GetDraftApplications;

public sealed record DraftApplicationSummary(Guid ApplicationId, Guid DogListingId, string DogName, DateTimeOffset? LastEditedAt);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record DraftApplicationsResponse(IReadOnlyList<DraftApplicationSummary> Items);
