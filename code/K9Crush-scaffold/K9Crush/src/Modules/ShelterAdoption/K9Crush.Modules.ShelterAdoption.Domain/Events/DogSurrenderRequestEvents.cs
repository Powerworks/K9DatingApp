namespace K9Crush.Modules.ShelterAdoption.Domain.Events;

/// <summary>ADR-031 event-sourcing retrofit, Phase 5/5 - one record per DogSurrenderRequest transition.</summary>
public sealed record DogSurrenderRequestedV1(
    Guid RequestedByOwnerId, string DogName, string Breed, int AgeInMonths,
    string ReasonForSurrender, string TemperamentNotes, string HealthNotes, DateTimeOffset RequestedAt);

public sealed record SurrenderRequestReviewedV1;

public sealed record AdditionalSurrenderDetailsRequestedV1(string Reason);

public sealed record AdditionalSurrenderDetailsSubmittedV1;

/// <summary>
/// ShelterAccountId is a disclosed gap-fill added for the
/// SurrenderingYourDogFullIntake chapter (v3 ENRICHMENT): the 9 Shelter
/// Staff intake-pipeline commands need to resolve which shelter owns a
/// given surrender request (for both the ownership gate and the
/// ShelterAccount.SurrenderIntakeMode == FullIntake check), but nothing
/// previously persisted it onto this aggregate - AcceptDogSurrenderHandler
/// only used the command's ShelterAccountId to create the DogListing.
/// Defaults to Guid.Empty so the existing DogSurrenderRequestTests.cs's
/// parameterless `Accept()` call keeps compiling unchanged.
/// </summary>
public sealed record DogSurrenderAcceptedV1(Guid ShelterAccountId = default);

public sealed record DogSurrenderDeclinedV1(string Reason);

/// <summary>
/// SurrenderingYourDogFullIntake chapter's "Complete Surrender Paperwork"
/// -> "Surrender Paperwork Completed". One of several independent
/// intake-pipeline steps gated only on Status == Accepted (they don't
/// depend on each other, per the yaml's per-step `given: Dog Surrender
/// Accepted` tests) - doesn't transition DogSurrenderRequest.Status.
/// </summary>
public sealed record SurrenderPaperworkCompletedV1(bool LegalTransferSigned, string OwnershipProofType);
