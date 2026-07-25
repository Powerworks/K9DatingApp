using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Api.ReadModels.GetPendingApplicationsQueue;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.IntegrationTests.ShelterAdoption;

/// <summary>
/// Layer 3 (TestingApproach.md) - GetPendingApplicationsQueueHandler calls
/// session.Query&lt;Application&gt;().Where(...).ToListAsync(), the LINQ path
/// Layer 2's IQuerySession mocks can't reach. Scoped to a per-test random
/// shelterAccountId, so safe to share ShelterAdoptionPostgresFixture.
/// Seeding now goes through Events.StartStream (ADR-031) rather than
/// session.Store, since both ShelterAccount and Application are
/// event-sourced.
/// </summary>
[Collection(ShelterAdoptionPostgresCollection.Name)]
public class GetPendingApplicationsQueueIntegrationTests(ShelterAdoptionPostgresFixture fixture)
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenShelterAccountDoesNotExist_ReturnsNotFound()
    {
        var shelterAccountId = Guid.NewGuid();
        await using var session = fixture.Store.LightweightSession();

        var result = await GetPendingApplicationsQueueHandler.Handle(shelterAccountId, BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerDoesNotOwnTheShelterAccount_ReturnsForbid()
    {
        var ownerId = Guid.NewGuid();
        var (shelterAccount, shelterAccountRequested) = ShelterAccount.RequestNew(ownerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
        await using (var seedSession = fixture.Store.LightweightSession())
        {
            seedSession.Events.StartStream<ShelterAccount>(shelterAccount.Id, shelterAccountRequested);
            await seedSession.SaveChangesAsync();
        }

        await using var session = fixture.Store.LightweightSession();
        var result = await GetPendingApplicationsQueueHandler.Handle(shelterAccount.Id, BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyOpenApplicationsForThatShelter()
    {
        var ownerId = Guid.NewGuid();
        var (shelterAccount, shelterAccountRequested) = ShelterAccount.RequestNew(ownerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
        var dogListingId = Guid.NewGuid();

        var (pendingApplication, pendingSubmitted) = Application.SubmitNew(Guid.NewGuid(), dogListingId, shelterAccount.Id, TestIntake.Default);
        var (withdrawnApplication, withdrawnSubmitted) = Application.SubmitNew(Guid.NewGuid(), dogListingId, shelterAccount.Id, TestIntake.Default);
        var withdrawnEvent = withdrawnApplication.Withdraw(); // not open - must be excluded
        var (otherShelterApplication, otherShelterSubmitted) = Application.SubmitNew(Guid.NewGuid(), dogListingId, Guid.NewGuid(), TestIntake.Default); // different shelter - must be excluded

        await using (var seedSession = fixture.Store.LightweightSession())
        {
            seedSession.Events.StartStream<ShelterAccount>(shelterAccount.Id, shelterAccountRequested);
            seedSession.Events.StartStream<Application>(pendingApplication.Id, pendingSubmitted);
            seedSession.Events.StartStream<Application>(withdrawnApplication.Id, withdrawnSubmitted, withdrawnEvent);
            seedSession.Events.StartStream<Application>(otherShelterApplication.Id, otherShelterSubmitted);
            await seedSession.SaveChangesAsync();
        }

        await using var session = fixture.Store.LightweightSession();
        var result = await GetPendingApplicationsQueueHandler.Handle(shelterAccount.Id, BuildUser(ownerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<PendingApplicationsQueueResponse>>();
        var response = ((Ok<PendingApplicationsQueueResponse>)result.Result).Value!;
        response.Items.Should().ContainSingle().Which.ApplicationId.Should().Be(pendingApplication.Id);
    }
}
