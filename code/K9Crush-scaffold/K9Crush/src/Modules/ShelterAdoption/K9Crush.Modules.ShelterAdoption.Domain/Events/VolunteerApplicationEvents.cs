namespace K9Crush.Modules.ShelterAdoption.Domain.Events;

/// <summary>
/// ADR-031 event-sourcing retrofit, Phase 5/5. One record per
/// VolunteerApplication transition, matching the entity's own domain
/// methods 1:1 - see MediaAssetEvents.cs (Phase 1) for the naming/location
/// convention.
/// </summary>
public sealed record VolunteerApplicationSubmittedV1(Guid ApplicantOwnerId, VolunteerAreaOfInterest AreaOfInterest, DateTimeOffset SubmittedAt);

public sealed record VolunteerApplicationReviewedV1;

public sealed record VolunteerApprovedV1;
