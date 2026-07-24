namespace K9Crush.Modules.ShelterAdoption.Domain.Events;

/// <summary>ADR-031 event-sourcing retrofit, Phase 5/5 - one record per FosterApplication transition.</summary>
public sealed record FosterApplicationSubmittedV1(Guid ApplicantOwnerId, HomeType HomeType, bool HasGarden, bool HasOtherPets, DateOnly AvailableFrom, DateTimeOffset SubmittedAt);

public sealed record FosterApplicationReviewedV1;

public sealed record FosterCaregiverApprovedV1;

public sealed record FosterApplicationRejectedV1(string Reason);
