using FluentAssertions;
using K9Crush.Modules.Identity.Contracts;
using K9Crush.Modules.ShelterAdoption.Api.Automations.WithdrawApplicationsOnAccountDeletionRequested;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.IntegrationTests.ShelterAdoption;

/// <summary>
/// Layer 3 (TestingApproach.md) - WithdrawApplicationsOnAccountDeletionRequestedHandler
/// calls session.Query&lt;Application&gt;().Where(...).ToListAsync(), the
/// LINQ path Layer 2's IDocumentSession mocks can't reach. Same pattern as
/// CancelApplicationsForRemovedListingIntegrationTests. Scoped to a
/// per-test random ApplicantOwnerId, so this is safe to share
/// ShelterAdoptionPostgresFixture via [Collection(...)] - not the global
/// "does any X exist" class of check that forced BootstrapAdmin/Chat's
/// tests onto per-instance IAsyncLifetime instead.
/// </summary>
[Collection(ShelterAdoptionPostgresCollection.Name)]
public class WithdrawApplicationsOnAccountDeletionRequestedIntegrationTests(ShelterAdoptionPostgresFixture fixture)
{
    private static AccountDeletionRequestedV1 BuildEvent(Guid ownerId) => new(
        EventId: Guid.NewGuid(), OccurredAt: DateTimeOffset.UtcNow, OwnerId: ownerId);

    [Fact]
    public async Task Handle_WithdrawsEveryOpenApplicationForThatOwner_AndLeavesOthersAlone()
    {
        var deletedOwnerId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();
        var shelterAccountId = Guid.NewGuid();
        var dogListingId = Guid.NewGuid();

        var openApplicationA = Application.Submit(deletedOwnerId, dogListingId, shelterAccountId, TestIntake.Default);
        openApplicationA.Review(); // UnderReview - open
        var openApplicationB = Application.Submit(deletedOwnerId, Guid.NewGuid(), shelterAccountId, TestIntake.Default); // Pending - open
        var alreadyWithdrawnApplication = Application.Submit(deletedOwnerId, Guid.NewGuid(), shelterAccountId, TestIntake.Default);
        alreadyWithdrawnApplication.Withdraw(); // not open - must be left alone
        var otherOwnersApplication = Application.Submit(otherOwnerId, dogListingId, shelterAccountId, TestIntake.Default);
        otherOwnersApplication.Review(); // open, but a different owner - must be left alone

        await using (var seedSession = fixture.Store.LightweightSession())
        {
            seedSession.Store(openApplicationA, openApplicationB, alreadyWithdrawnApplication, otherOwnersApplication);
            await seedSession.SaveChangesAsync();
        }

        await using var session = fixture.Store.LightweightSession();
        await WithdrawApplicationsOnAccountDeletionRequestedHandler.Handle(
            BuildEvent(deletedOwnerId), session, CancellationToken.None);

        await using var verifySession = fixture.Store.LightweightSession();
        (await verifySession.LoadAsync<Application>(openApplicationA.Id))!.Status.Should().Be(ApplicationStatus.Withdrawn);
        (await verifySession.LoadAsync<Application>(openApplicationB.Id))!.Status.Should().Be(ApplicationStatus.Withdrawn);
        (await verifySession.LoadAsync<Application>(alreadyWithdrawnApplication.Id))!.Status.Should().Be(ApplicationStatus.Withdrawn, "already withdrawn - must not error or change");
        (await verifySession.LoadAsync<Application>(otherOwnersApplication.Id))!.Status.Should().Be(ApplicationStatus.UnderReview, "a different owner's application - must not be touched");
    }

    [Fact]
    public async Task Handle_WhenOwnerHasNoOpenApplications_DoesNothing()
    {
        var ownerId = Guid.NewGuid();
        await using var session = fixture.Store.LightweightSession();

        await WithdrawApplicationsOnAccountDeletionRequestedHandler.Handle(
            BuildEvent(ownerId), session, CancellationToken.None);

        // No exception, no applications to assert on - this is the "nothing to withdraw" no-op path.
    }
}
