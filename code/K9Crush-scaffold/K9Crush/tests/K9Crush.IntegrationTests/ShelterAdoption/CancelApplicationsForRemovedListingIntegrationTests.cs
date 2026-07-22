using FluentAssertions;
using NSubstitute;
using Wolverine;
using K9Crush.Modules.ShelterAdoption.Api.Automations.CancelApplicationsForRemovedListing;
using K9Crush.Modules.ShelterAdoption.Contracts;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.IntegrationTests.ShelterAdoption;

/// <summary>
/// Layer 3 (TestingApproach.md) - CancelApplicationsForRemovedListingHandler
/// calls session.Query&lt;Application&gt;().Where(...).ToListAsync(), the
/// LINQ path Layer 2's IDocumentSession mocks can't reach. IMessageBus is
/// still mocked (NSubstitute) even here - nothing about verifying which
/// messages got cascaded needs a real broker, only the Application query/
/// mutation needs real Postgres.
/// </summary>
[Collection(ShelterAdoptionPostgresCollection.Name)]
public class CancelApplicationsForRemovedListingIntegrationTests(ShelterAdoptionPostgresFixture fixture)
{
    private static DogListingRemovedV1 BuildEvent(Guid dogListingId, Guid shelterAccountId) => new(
        EventId: Guid.NewGuid(), OccurredAt: DateTimeOffset.UtcNow,
        DogListingId: dogListingId, ShelterAccountId: shelterAccountId, DogName: "Biscuit");

    [Fact]
    public async Task Handle_CancelsEveryOpenApplicationForTheListing_AndCascadesOnePerApplicant()
    {
        var shelterAccountId = Guid.NewGuid();
        var dogListingId = Guid.NewGuid();
        var applicantA = Guid.NewGuid();
        var applicantB = Guid.NewGuid();
        var otherListingId = Guid.NewGuid();

        var openApplicationA = Application.Submit(applicantA, dogListingId, shelterAccountId);
        openApplicationA.Review(); // UnderReview - open
        var openApplicationB = Application.Submit(applicantB, dogListingId, shelterAccountId); // Pending - open
        var withdrawnApplication = Application.Submit(Guid.NewGuid(), dogListingId, shelterAccountId);
        withdrawnApplication.Withdraw(); // not open - must be left alone
        var unrelatedApplication = Application.Submit(Guid.NewGuid(), otherListingId, shelterAccountId);
        unrelatedApplication.Review(); // open, but a different listing - must be left alone

        await using (var seedSession = fixture.Store.LightweightSession())
        {
            seedSession.Store(openApplicationA, openApplicationB, withdrawnApplication, unrelatedApplication);
            await seedSession.SaveChangesAsync();
        }

        var bus = Substitute.For<IMessageBus>();
        await using var session = fixture.Store.LightweightSession();
        await CancelApplicationsForRemovedListingHandler.Handle(
            BuildEvent(dogListingId, shelterAccountId), session, bus, CancellationToken.None);

        await using var verifySession = fixture.Store.LightweightSession();
        (await verifySession.LoadAsync<Application>(openApplicationA.Id))!.Status.Should().Be(ApplicationStatus.ClosedDogNoLongerAvailable);
        (await verifySession.LoadAsync<Application>(openApplicationB.Id))!.Status.Should().Be(ApplicationStatus.ClosedDogNoLongerAvailable);
        (await verifySession.LoadAsync<Application>(withdrawnApplication.Id))!.Status.Should().Be(ApplicationStatus.Withdrawn, "not open - must not be touched");
        (await verifySession.LoadAsync<Application>(unrelatedApplication.Id))!.Status.Should().Be(ApplicationStatus.UnderReview, "different listing - must not be touched");

        await bus.Received(1).PublishAsync(
            Arg.Is<ApplicationCancelledV1>(e => e.ApplicationId == openApplicationA.Id && e.ApplicantOwnerId == applicantA),
            Arg.Any<DeliveryOptions?>());
        await bus.Received(1).PublishAsync(
            Arg.Is<ApplicationCancelledV1>(e => e.ApplicationId == openApplicationB.Id && e.ApplicantOwnerId == applicantB),
            Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task Handle_WhenNoOpenApplicationsExistForTheListing_DoesNothing()
    {
        var dogListingId = Guid.NewGuid();
        var bus = Substitute.For<IMessageBus>();
        await using var session = fixture.Store.LightweightSession();

        await CancelApplicationsForRemovedListingHandler.Handle(
            BuildEvent(dogListingId, Guid.NewGuid()), session, bus, CancellationToken.None);

        await bus.DidNotReceiveWithAnyArgs().PublishAsync(default(ApplicationCancelledV1)!, default);
    }
}
