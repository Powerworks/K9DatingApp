using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.ShelterAdoption.Contracts;

/// <summary>
/// Published when a shelter rejects an application (see
/// RejectApplicationHandler) - the emlang yaml's "Send Rejection Reason"
/// -> "Rejection Reason Sent" cascade, previously deferred pending a
/// Notifications module (ADR-027) to consume it.
/// DogName is denormalized from DogListing (same-module lookup) since
/// Notifications can only see this module's Contracts, never its Domain.
/// </summary>
public sealed record ApplicationRejectedV1(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid ApplicationId,
    Guid ApplicantOwnerId,
    Guid DogListingId,
    string DogName,
    string RejectionReason) : IIntegrationEvent;
