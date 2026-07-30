using Marten;
using Wolverine;
using K9Crush.Modules.ShelterAdoption.Contracts;
using K9Crush.Modules.ShelterAdoption.Domain;

namespace K9Crush.Modules.ShelterAdoption.Api.Automations.CancelApplicationsForRemovedListing;

/// <summary>
/// Automation slice (ADR-028): the emlang yaml's ShelterManagingListings
/// chapter's "Cancel Applications For Removed Listing" -> "Applications
/// Cancelled For Removed Listing", given "Dog Listing Removed". Same-
/// module trigger via ShelterAdoptionModule's own IntegrationEventQueueName
/// (document-store module, no domain event stream to subscribe to the
/// usual way).
///
/// Loads all Applications for the removed listing, then filters to
/// IsOpen in memory (same pattern as SubmitApplicationHandler's own
/// LINQ - Marten's provider isn't asked to translate the IsOpen property
/// getter itself, only the plain DogListingId equality). Cascades one
/// ApplicationCancelledV1 per affected applicant - the trigger for
/// Notifications' NotifyOnApplicationCancelledHandler.
/// </summary>
public static class CancelApplicationsForRemovedListingHandler
{
    public static async Task Handle(
        DogListingRemovedV1 integrationEvent,
        IDocumentSession session,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var applications = await session.Query<Application>()
            .Where(x => x.DogListingId == integrationEvent.DogListingId)
            .ToListAsync(cancellationToken);

        var openApplicationIds = applications.Where(x => x.IsOpen).Select(x => x.Id).ToList();
        if (openApplicationIds.Count == 0)
            return;

        var openApplications = new List<Application>();
        foreach (var applicationId in openApplicationIds)
        {
            var stream = await session.Events.FetchForWriting<Application>(applicationId, cancellationToken);
            var application = stream.Aggregate!;
            stream.AppendOne(application.CancelDogNoLongerAvailable());
            openApplications.Add(application);
        }

        await session.SaveChangesAsync(cancellationToken);

        foreach (var application in openApplications)
        {
            await bus.PublishAsync(new ApplicationCancelledV1(
                EventId: Guid.NewGuid(),
                OccurredAt: DateTimeOffset.UtcNow,
                ApplicationId: application.Id,
                ApplicantOwnerId: application.ApplicantOwnerId,
                DogListingId: integrationEvent.DogListingId,
                DogName: integrationEvent.DogName));
        }
    }
}
