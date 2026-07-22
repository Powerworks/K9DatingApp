using FluentAssertions;
using NSubstitute;
using Wolverine;
using K9Crush.Modules.ShelterAdoption.Api.Automations.NotifyApplicantsOfListingChange;
using K9Crush.Modules.ShelterAdoption.Contracts;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.IntegrationTests.ShelterAdoption;

/// <summary>
/// Layer 3 (TestingApproach.md) - NotifyApplicantsOfListingChangeHandler
/// calls session.Query&lt;Application&gt;().Where(...).ToListAsync(), the
/// LINQ path Layer 2's IDocumentSession mocks can't reach.
/// </summary>
[Collection(ShelterAdoptionPostgresCollection.Name)]
public class NotifyApplicantsOfListingChangeIntegrationTests(ShelterAdoptionPostgresFixture fixture)
{
    private static DogListingSignificantlyEditedV1 BuildEvent(Guid dogListingId, Guid shelterAccountId) => new(
        EventId: Guid.NewGuid(), OccurredAt: DateTimeOffset.UtcNow,
        DogListingId: dogListingId, ShelterAccountId: shelterAccountId, DogName: "Biscuit");

    [Fact]
    public async Task Handle_NotifiesEveryOpenApplicantForTheListing_WithoutChangingApplicationStatus()
    {
        var shelterAccountId = Guid.NewGuid();
        var dogListingId = Guid.NewGuid();
        var applicantA = Guid.NewGuid();
        var withdrawnApplicant = Guid.NewGuid();

        var openApplication = Application.Submit(applicantA, dogListingId, shelterAccountId);
        openApplication.Review();
        var withdrawnApplication = Application.Submit(withdrawnApplicant, dogListingId, shelterAccountId);
        withdrawnApplication.Withdraw();

        await using (var seedSession = fixture.Store.LightweightSession())
        {
            seedSession.Store(openApplication, withdrawnApplication);
            await seedSession.SaveChangesAsync();
        }

        var bus = Substitute.For<IMessageBus>();
        await using var session = fixture.Store.QuerySession();
        await NotifyApplicantsOfListingChangeHandler.Handle(
            BuildEvent(dogListingId, shelterAccountId), session, bus, CancellationToken.None);

        await bus.Received(1).PublishAsync(
            Arg.Is<ApplicationListingChangedV1>(e => e != null && e.ApplicationId == openApplication.Id && e.ApplicantOwnerId == applicantA),
            Arg.Any<DeliveryOptions?>());
        await bus.DidNotReceive().PublishAsync(
            Arg.Is<ApplicationListingChangedV1>(e => e != null && e.ApplicationId == withdrawnApplication.Id),
            Arg.Any<DeliveryOptions?>());

        await using var verifySession = fixture.Store.LightweightSession();
        (await verifySession.LoadAsync<Application>(openApplication.Id))!.Status.Should().Be(ApplicationStatus.UnderReview, "purely informational - status must be unchanged");
    }
}
