namespace K9Crush.Modules.ShelterAdoption.Domain.Events;

/// <summary>ADR-031 event-sourcing retrofit, Phase 5/5 - one record per DogSurrenderRequest transition.</summary>
public sealed record DogSurrenderRequestedV1(
    Guid RequestedByOwnerId, string DogName, string Breed, int AgeInMonths,
    string ReasonForSurrender, string TemperamentNotes, string HealthNotes, DateTimeOffset RequestedAt);

public sealed record SurrenderRequestReviewedV1;

public sealed record AdditionalSurrenderDetailsRequestedV1(string Reason);

public sealed record AdditionalSurrenderDetailsSubmittedV1;

/// <summary>
/// ShelterAccountId is a disclosed gap-fill (SurrenderingYourDogFullIntake
/// chapter): the yaml's "Dog Surrender Accepted" event never re-lists it as
/// a prop, but the FullIntake pipeline's own steps (Add To Waiting List
/// etc.) need to know which shelter owns the request for their
/// Shelter-policy ownership check, and AcceptDogSurrenderHandler already
/// has it (as AcceptDogSurrenderRequest.ShelterAccountId) - just wasn't
/// being persisted onto the aggregate before. Defaults to keep
/// DogSurrenderRequestTests.cs's existing `request.Accept();` call
/// compiling (CLAUDE.md: don't change existing test files).
/// </summary>
public sealed record DogSurrenderAcceptedV1(Guid ShelterAccountId = default);

public sealed record DogSurrenderDeclinedV1(string Reason);

/// <summary>SurrenderingYourDogFullIntake chapter's "Add To Waiting List" -> "Added To Waiting List".</summary>
public sealed record AddedToWaitingListV1(int WaitlistPosition);
