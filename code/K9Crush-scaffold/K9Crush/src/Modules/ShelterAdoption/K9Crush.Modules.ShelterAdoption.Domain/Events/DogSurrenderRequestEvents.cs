namespace K9Crush.Modules.ShelterAdoption.Domain.Events;

/// <summary>ADR-031 event-sourcing retrofit, Phase 5/5 - one record per DogSurrenderRequest transition.</summary>
public sealed record DogSurrenderRequestedV1(
    Guid RequestedByOwnerId, string DogName, string Breed, int AgeInMonths,
    string ReasonForSurrender, string TemperamentNotes, string HealthNotes, DateTimeOffset RequestedAt);

public sealed record SurrenderRequestReviewedV1;

public sealed record AdditionalSurrenderDetailsRequestedV1(string Reason);

public sealed record AdditionalSurrenderDetailsSubmittedV1;

public sealed record DogSurrenderAcceptedV1;

public sealed record DogSurrenderDeclinedV1(string Reason);

/// <summary>SurrenderingYourDogFullIntake chapter - "Perform Behavior Test" -> "Behavior Test Completed".</summary>
public sealed record BehaviorTestCompletedV1(Guid PerformedBy, bool SuitableForRehoming, string BehaviorNotes);
