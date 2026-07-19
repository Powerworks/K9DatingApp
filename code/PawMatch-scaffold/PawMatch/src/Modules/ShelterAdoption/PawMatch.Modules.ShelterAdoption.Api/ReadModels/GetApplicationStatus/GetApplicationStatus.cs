namespace PawMatch.Modules.ShelterAdoption.Api.ReadModels.GetApplicationStatus;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record ApplicationStatusResponse(
    Guid ApplicationId,
    Guid DogListingId,
    string Status,
    string? AdditionalDetailsRequestReason,
    string? RejectionReason);
