using FluentAssertions;
using K9Crush.Modules.ShelterAdoption.Api.ReadModels.GetFosterApplicationsQueue;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.IntegrationTests.ShelterAdoption;

/// <summary>
/// Layer 3 (TestingApproach.md) - GetFosterApplicationsQueueHandler calls
/// session.Query&lt;FosterApplication&gt;().ToListAsync() with NO filter at
/// all - a genuinely global, unscoped query, same class of check that
/// forced GetSurrenderReviewQueueIntegrationTests/GetAdoptionListingsIntegrationTests
/// onto their own dedicated per-instance IAsyncLifetime container instead
/// of sharing one via [Collection(...)]. Same fix applied here up front.
/// Seeding now goes through Events.StartStream (ADR-031) rather than
/// session.Store, since FosterApplication is event-sourced.
/// </summary>
public class GetFosterApplicationsQueueIntegrationTests : IAsyncLifetime
{
    private readonly ShelterAdoptionPostgresFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task Handle_WhenNoApplicationsExist_ReturnsEmptyList()
    {
        await using var session = _fixture.Store.LightweightSession();

        var response = await GetFosterApplicationsQueueHandler.Handle(session, CancellationToken.None);

        response.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsEveryFosterApplicationRegardlessOfStatus()
    {
        var (submitted, submittedEvent) = FosterApplication.ApplyNew(Guid.NewGuid(), HomeType.House, hasGarden: true, hasOtherPets: false, new DateOnly(2026, 8, 1));
        var (approved, approvedSubmittedEvent) = FosterApplication.ApplyNew(Guid.NewGuid(), HomeType.Apartment, hasGarden: false, hasOtherPets: true, new DateOnly(2026, 9, 1));
        var reviewedEvent = approved.Review();
        var approvedEvent = approved.Approve();

        await using (var seedSession = _fixture.Store.LightweightSession())
        {
            seedSession.Events.StartStream<FosterApplication>(submitted.Id, submittedEvent);
            seedSession.Events.StartStream<FosterApplication>(approved.Id, approvedSubmittedEvent, reviewedEvent, approvedEvent);
            await seedSession.SaveChangesAsync();
        }

        await using var session = _fixture.Store.LightweightSession();
        var response = await GetFosterApplicationsQueueHandler.Handle(session, CancellationToken.None);

        response.Items.Should().HaveCount(2);
        response.Items.Should().Contain(x => x.FosterApplicationId == submitted.Id && x.Status == nameof(FosterApplicationStatus.Submitted));
        response.Items.Should().Contain(x => x.FosterApplicationId == approved.Id && x.Status == nameof(FosterApplicationStatus.Approved));
    }
}
