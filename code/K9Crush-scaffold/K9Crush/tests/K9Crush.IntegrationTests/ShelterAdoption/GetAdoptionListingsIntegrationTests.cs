using FluentAssertions;
using Marten;
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
/// fix applied here up front. Seeding now goes through Events.StartStream
/// (ADR-031) rather than session.Store, since DogListing is event-sourced.
/// </summary>
public class GetAdoptionListingsIntegrationTests : IAsyncLifetime
{
    private readonly ShelterAdoptionPostgresFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    private static void SeedAvailable(IDocumentSession session, DogListing dogListing, K9Crush.Modules.ShelterAdoption.Domain.Events.DogListingAddedV1 addedEvent)
    {
        var statusEvent = dogListing.UpdateStatus(DogListingStatus.Available);
        session.Events.StartStream<DogListing>(dogListing.Id, addedEvent, statusEvent);
    }

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
        var (listingA, addedA) = DogListing.AddNew(Guid.NewGuid(), "Biscuit", "Labrador", 36, "Friendly");
        var (listingB, addedB) = DogListing.AddNew(Guid.NewGuid(), "Max", "Beagle", 24, "Playful");

        await using (var seedSession = _fixture.Store.LightweightSession())
        {
            SeedAvailable(seedSession, listingA, addedA);
            SeedAvailable(seedSession, listingB, addedB);
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
        var (available, addedAvailable) = DogListing.AddNew(Guid.NewGuid(), "Biscuit", "Labrador", 36, "Friendly");
        var (notReadyYet, addedNotReadyYet) = DogListing.AddNew(Guid.NewGuid(), "Max", "Beagle", 24, "Playful"); // default status
        var (inFoster, addedInFoster) = DogListing.AddNew(Guid.NewGuid(), "Rex", "Terrier", 12, "Energetic");
        var (adopted, addedAdopted) = DogListing.AddNew(Guid.NewGuid(), "Luna", "Poodle", 48, "Calm");

        await using (var seedSession = _fixture.Store.LightweightSession())
        {
            SeedAvailable(seedSession, available, addedAvailable);
            seedSession.Events.StartStream<DogListing>(notReadyYet.Id, addedNotReadyYet);
            seedSession.Events.StartStream<DogListing>(inFoster.Id, addedInFoster, inFoster.UpdateStatus(DogListingStatus.InFoster));
            seedSession.Events.StartStream<DogListing>(adopted.Id, addedAdopted, adopted.UpdateStatus(DogListingStatus.Adopted));
            await seedSession.SaveChangesAsync();
        }

        await using var session = _fixture.Store.LightweightSession();
        var response = await GetAdoptionListingsHandler.Handle(session, CancellationToken.None);

        response.Items.Select(x => x.DogListingId).Should().BeEquivalentTo([available.Id]);
    }
}
