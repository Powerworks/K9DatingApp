using Marten;
using Wolverine;
using K9Crush.Modules.ShelterAdoption.Contracts;
using K9Crush.Modules.ShelterAdoption.Domain;

namespace K9Crush.Modules.ShelterAdoption.Api.Automations.NotifyApplicantsOfListingChange;

/// <summary>
/// Automation slice (ADR-028): the emlang yaml's ShelterManagingListings
/// chapter's "Notify Applicant Of Listing Change" -> "Applicant Notified
/// Of Listing Change", given "Dog Listing Edited" with significantChange.
/// Same-module trigger, same shape as
/// CancelApplicationsForRemovedListingHandler, but purely informational -
/// doesn't touch Application.Status at all, just finds who to notify.
/// </summary>
public static class NotifyApplicantsOfListingChangeHandler
{
    public static async Task Handle(
        DogListingSignificantlyEditedV1 integrationEvent,
        IQuerySession session,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var applications = await session.Query<Application>()
            .Where(x => x.DogListingId == integrationEvent.DogListingId)
            .ToListAsync(cancellationToken);

        var openApplications = applications.Where(x => x.IsOpen);

        foreach (var application in openApplications)
        {
            await bus.PublishAsync(new ApplicationListingChangedV1(
                EventId: Guid.NewGuid(),
                OccurredAt: DateTimeOffset.UtcNow,
                ApplicationId: application.Id,
                ApplicantOwnerId: application.ApplicantOwnerId,
                DogListingId: integrationEvent.DogListingId,
                DogName: integrationEvent.DogName));
        }
    }
}
