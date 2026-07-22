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
        var submitted = FosterApplication.Apply(Guid.NewGuid(), HomeType.House, hasGarden: true, hasOtherPets: false, new DateOnly(2026, 8, 1));
        var approved = FosterApplication.Apply(Guid.NewGuid(), HomeType.Apartment, hasGarden: false, hasOtherPets: true, new DateOnly(2026, 9, 1));
        approved.Review();
        approved.Approve();

        await using (var seedSession = _fixture.Store.LightweightSession())
        {
            seedSession.Store(submitted, approved);
            await seedSession.SaveChangesAsync();
        }

        await using var session = _fixture.Store.LightweightSession();
        var response = await GetFosterApplicationsQueueHandler.Handle(session, CancellationToken.None);

        response.Items.Should().HaveCount(2);
        response.Items.Should().Contain(x => x.FosterApplicationId == submitted.Id && x.Status == nameof(FosterApplicationStatus.Submitted));
        response.Items.Should().Contain(x => x.FosterApplicationId == approved.Id && x.Status == nameof(FosterApplicationStatus.Approved));
    }
}
