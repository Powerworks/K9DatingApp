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
    public async Task Handle_ReturnsEveryAvailableListingAcrossEveryShelter()
    {
        var listingA = DogListing.Create(Guid.NewGuid(), "Biscuit", "Labrador", 36, "Friendly");
        listingA.UpdateStatus(DogListingStatus.Available);
        var listingB = DogListing.Create(Guid.NewGuid(), "Max", "Beagle", 24, "Playful");
        listingB.UpdateStatus(DogListingStatus.Available);

        await using (var seedSession = _fixture.Store.LightweightSession())
        {
            seedSession.Store(listingA, listingB);
            await seedSession.SaveChangesAsync();
        }

        await using var session = _fixture.Store.LightweightSession();
        var response = await GetAdoptionListingsHandler.Handle(session, CancellationToken.None);

        response.Items.Select(x => x.DogListingId).Should().BeEquivalentTo([listingA.Id, listingB.Id]);
    }

    /// <summary>
    /// v3 ENRICHMENT (Spec/K9CRUSH.emlang.v3.yaml's ShelterManagingListings
    /// chapter) - non-Available listings (freshly added, in foster,
    /// pending, or already adopted) shouldn't surface in the public
    /// marketplace browse.
    /// </summary>
    [Fact]
    public async Task Handle_ExcludesListingsThatAreNotAvailable()
    {
        var available = DogListing.Create(Guid.NewGuid(), "Biscuit", "Labrador", 36, "Friendly");
        available.UpdateStatus(DogListingStatus.Available);
        var notReadyYet = DogListing.Create(Guid.NewGuid(), "Max", "Beagle", 24, "Playful"); // default status
        var inFoster = DogListing.Create(Guid.NewGuid(), "Rex", "Terrier", 12, "Energetic");
        inFoster.UpdateStatus(DogListingStatus.InFoster);
        var adopted = DogListing.Create(Guid.NewGuid(), "Luna", "Poodle", 48, "Calm");
        adopted.UpdateStatus(DogListingStatus.Adopted);

        await using (var seedSession = _fixture.Store.LightweightSession())
        {
            seedSession.Store(available, notReadyYet, inFoster, adopted);
            await seedSession.SaveChangesAsync();
        }

        await using var session = _fixture.Store.LightweightSession();
        var response = await GetAdoptionListingsHandler.Handle(session, CancellationToken.None);

        response.Items.Select(x => x.DogListingId).Should().BeEquivalentTo([available.Id]);
    }
}
