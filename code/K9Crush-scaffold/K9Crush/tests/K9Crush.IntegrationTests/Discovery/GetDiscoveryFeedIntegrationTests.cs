using FluentAssertions;
using K9Crush.Modules.Discovery.Api.ReadModels.GetDiscoveryFeed;
using K9Crush.Modules.Profiles.Contracts;
using Xunit;

namespace K9Crush.IntegrationTests.Discovery;

/// <summary>
/// Layer 3 (TestingApproach.md) - GetDiscoveryFeedHandler calls
/// session.Query&lt;DiscoveryFeedItem&gt;().ToListAsync(), the LINQ path
/// Layer 2's IDocumentSession mocks can't reach. Covers the whole
/// DogProfileCreatedV1 -> DogProfileCreatedProjectorHandler ->
/// GetDiscoveryFeedHandler chain end to end, confirming Name/MatchType/
/// DistanceMiles (TheWindowShopper's "Nearby Dogs Preview" view props)
/// actually come through, not just that each piece compiles in isolation.
/// </summary>
[Collection(DiscoveryPostgresCollection.Name)]
public class GetDiscoveryFeedIntegrationTests(DiscoveryPostgresFixture fixture)
{
    [Fact]
    public async Task Feed_AfterADogProfileIsPublished_ReturnsItWithinRadiusAsDogToDog()
    {
        var dogProfileId = Guid.NewGuid();
        await using (var session = fixture.Store.LightweightSession())
        {
            await DogProfileCreatedProjectorHandler.Handle(
                new DogProfileCreatedV1(
                    EventId: Guid.NewGuid(),
                    OccurredAt: DateTimeOffset.UtcNow,
                    DogProfileId: dogProfileId,
                    OwnerId: Guid.NewGuid(),
                    Name: "Luna",
                    Breed: "Beagle mix",
                    Latitude: 45.5,
                    Longitude: -122.6),
                session,
                CancellationToken.None);
        }

        await using var querySession = fixture.Store.LightweightSession();
        var response = await GetDiscoveryFeedHandler.Handle(
            latitude: 45.5, longitude: -122.6, radiusMiles: 50, querySession, CancellationToken.None);

        var entry = response.Items.Should().ContainSingle(x => x.DogProfileId == dogProfileId).Subject;
        entry.Name.Should().Be("Luna");
        entry.Breed.Should().Be("Beagle mix");
        entry.MatchType.Should().Be("dog_to_dog");
        entry.DistanceMiles.Should().BeApproximately(0, 0.01);
    }

    [Fact]
    public async Task Feed_ExcludesDogsOutsideTheRequestedRadius()
    {
        var dogProfileId = Guid.NewGuid();
        await using (var session = fixture.Store.LightweightSession())
        {
            await DogProfileCreatedProjectorHandler.Handle(
                new DogProfileCreatedV1(
                    EventId: Guid.NewGuid(),
                    OccurredAt: DateTimeOffset.UtcNow,
                    DogProfileId: dogProfileId,
                    OwnerId: Guid.NewGuid(),
                    Name: "Far Away Fido",
                    Breed: "Mixed",
                    Latitude: 51.5, // London
                    Longitude: -0.1),
                session,
                CancellationToken.None);
        }

        await using var querySession = fixture.Store.LightweightSession();
        var response = await GetDiscoveryFeedHandler.Handle(
            latitude: 45.5, longitude: -122.6, radiusMiles: 50, querySession, CancellationToken.None); // Portland, OR

        response.Items.Should().NotContain(x => x.DogProfileId == dogProfileId);
    }
}
