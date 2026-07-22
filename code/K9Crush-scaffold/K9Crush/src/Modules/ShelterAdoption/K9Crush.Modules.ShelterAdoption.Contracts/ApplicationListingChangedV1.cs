using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.ShelterAdoption.Contracts;

/// <summary>
/// Published once per affected applicant by
/// Automations/NotifyApplicantsOfListingChange (itself triggered by
/// DogListingSignificantlyEditedV1) - the emlang yaml's "Notify Applicant
/// Of Listing Change" -> "Applicant Notified Of Listing Change". Consumed
/// by Notifications (NotifyOnApplicationListingChangedHandler). Doesn't
/// change the Application's own status - purely informational.
/// </summary>
public sealed record ApplicationListingChangedV1(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid ApplicationId,
    Guid ApplicantOwnerId,
    Guid DogListingId,
    string DogName) : IIntegrationEvent;
