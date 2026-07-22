using Marten;
using K9Crush.Modules.Identity.Contracts;
using K9Crush.Modules.ShelterAdoption.Domain;

namespace K9Crush.Modules.ShelterAdoption.Api.Automations.WithdrawApplicationsOnAccountDeletionRequested;

/// <summary>
/// Automation slice: the emlang yaml's AccountProfileSettings chapter's
/// "Withdraw Applications Before Deletion" -> "Applications Withdrawn
/// Before Deletion", given Identity's cross-module AccountDeletionRequestedV1
/// (see that contract's own doc comment for what's deliberately not built
/// alongside it - the "Open Items Flagged" count display and the
/// shelter-side "Flag Active Listings" branch).
///
/// Silently withdraws every open application for the deleted-account
/// owner - no cascade back to Identity or a notification to the shelter.
/// The yaml doesn't show a "Notify Shelter" step for this specific chain
/// the way ShelterManagingListings' DogListingRemoved chain does, so none
/// is built; reusing ApplicationCancelledV1 here would also be
/// semantically wrong (it's documented as specifically triggered by a
/// listing being removed, and Withdrawn is a distinct ApplicationStatus
/// from ClosedDogNoLongerAvailable).
/// </summary>
public static class WithdrawApplicationsOnAccountDeletionRequestedHandler
{
    public static async Task Handle(
        AccountDeletionRequestedV1 integrationEvent,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var applications = await session.Query<Application>()
            .Where(x => x.ApplicantOwnerId == integrationEvent.OwnerId)
            .ToListAsync(cancellationToken);

        var openApplications = applications.Where(x => x.IsOpen).ToList();
        if (openApplications.Count == 0)
            return;

        foreach (var application in openApplications)
        {
            application.Withdraw();
            session.Store(application);
        }

        await session.SaveChangesAsync(cancellationToken);
    }
}
