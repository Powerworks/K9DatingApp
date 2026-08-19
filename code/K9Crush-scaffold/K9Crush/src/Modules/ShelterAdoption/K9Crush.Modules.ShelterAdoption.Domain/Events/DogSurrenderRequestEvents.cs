namespace K9Crush.Modules.ShelterAdoption.Domain.Events;

/// <summary>ADR-031 event-sourcing retrofit, Phase 5/5 - one record per DogSurrenderRequest transition.</summary>
public sealed record DogSurrenderRequestedV1(
    Guid RequestedByOwnerId, string DogName, string Breed, int AgeInMonths,
    string ReasonForSurrender, string TemperamentNotes, string HealthNotes, DateTimeOffset RequestedAt);

public sealed record SurrenderRequestReviewedV1;

public sealed record AdditionalSurrenderDetailsRequestedV1(string Reason);

public sealed record AdditionalSurrenderDetailsSubmittedV1;

/// <summary>
/// ShelterAccountId is a disclosed gap-fill (see AcceptDogSurrenderRequest's
/// own doc comment) - carried onto the event/entity so the
/// SurrenderingYourDogFullIntake pipeline's later steps (Schedule Intake
/// Appointment, Complete Surrender Paperwork, etc.) can resolve shelter
/// ownership without a second lookup field on every downstream command.
/// </summary>
public sealed record DogSurrenderAcceptedV1(Guid ShelterAccountId);

public sealed record DogSurrenderDeclinedV1(string Reason);

/// <summary>SurrenderingYourDogFullIntake chapter's "Schedule Intake
/// Appointment" -> "Intake Appointment Scheduled".</summary>
public sealed record IntakeAppointmentScheduledV1(DateOnly AppointmentDate);
