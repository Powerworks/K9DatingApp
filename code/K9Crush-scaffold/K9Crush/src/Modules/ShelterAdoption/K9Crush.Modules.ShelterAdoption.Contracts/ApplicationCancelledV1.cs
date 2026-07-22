using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.ShelterAdoption.Contracts;

/// <summary>
/// Published once per affected applicant by
/// Automations/CancelApplicationsForRemovedListing (itself triggered by
/// DogListingRemovedV1) - the emlang yaml's "Cancel Applications For
/// Removed Listing" -> "Notify Applicants Of Cancellation" chain's final
/// leg. Consumed by Notifications (NotifyOnApplicationCancelledHandler).
/// </summary>
public sealed record ApplicationCancelledV1(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid ApplicationId,
    Guid ApplicantOwnerId,
    Guid DogListingId,
    string DogName) : IIntegrationEvent;
