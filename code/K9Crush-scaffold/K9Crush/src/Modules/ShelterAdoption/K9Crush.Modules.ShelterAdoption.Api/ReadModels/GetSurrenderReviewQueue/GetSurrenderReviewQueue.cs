namespace K9Crush.Modules.ShelterAdoption.Api.ReadModels.GetSurrenderReviewQueue;

public sealed record SurrenderRequestSummary(Guid SurrenderRequestId, string DogName, Guid RequestedByOwnerId, string Status);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record SurrenderReviewQueueResponse(IReadOnlyList<SurrenderRequestSummary> Items);
