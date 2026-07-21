using FluentAssertions;
using K9Crush.Modules.ShelterAdoption.Api.ReadModels.GetAdoptionListings;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.IntegrationTests.ShelterAdoption;

/// <summary>
/// Layer 3 (TestingApproach.md) - GetAdoptionListingsHandler calls
/// session.Query&lt;DogListing&gt;().ToListAsync() with NO filter at all (it's
/// the public marketplace browse - every listing across every shelter) -
/// a genuinely global, unscoped query, the same class of check that
/// forced BootstrapAdminIntegrationTests/ViewNotificationTemplatesIntegrationTests/
/// GetFeedbackInboxIntegrationTests onto their own dedicated per-instance
/// IAsyncLifetime container instead of sharing one via [Collection(...)] -
/// see those test classes' doc comments for the full writeup of why. Same
/// fix applied here up front.
/// </summary>
public class GetAdoptionListingsIntegrationTests : IAsyncLifetime
{
    private readonly ShelterAdoptionPostgresFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task Handle_WhenNoListingsExist_ReturnsEmptyList()
    {
        await using var session = _fixture.Store.LightweightSession();

        var response = await GetAdoptionListingsHandler.Handle(session, CancellationToken.None);

        response.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsEveryListingAcrossEveryShelter()
    {
        var listingA = DogListing.Create(Guid.NewGuid(), "Biscuit", "Labrador", 36, "Friendly");
        var listingB = DogListing.Create(Guid.NewGuid(), "Max", "Beagle", 24, "Playful");

        await using (var seedSession = _fixture.Store.LightweightSession())
        {
            seedSession.Store(listingA, listingB);
            await seedSession.SaveChangesAsync();
        }

        await using var session = _fixture.Store.LightweightSession();
        var response = await GetAdoptionListingsHandler.Handle(session, CancellationToken.None);

        response.Items.Select(x => x.DogListingId).Should().BeEquivalentTo([listingA.Id, listingB.Id]);
    }
}
