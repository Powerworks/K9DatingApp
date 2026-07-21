using FluentAssertions;
using K9Crush.Modules.Discovery.Api.Automations.DetectMutualMatch;
using K9Crush.Modules.Discovery.Domain;
using K9Crush.Modules.Discovery.Domain.Events;
using Xunit;

namespace K9Crush.IntegrationTests.Discovery;

/// <summary>
/// Layer 3 (TestingApproach.md) - DetectMutualMatchHandler needs a real
/// event store (AggregateStreamAsync). Covers the owner-enrichment
/// added alongside NotifyOnMatchHandler: MatchCreatedV1 now carries
/// OwnerAId/OwnerBId, resolved from this module's own DiscoveryFeedItem.
/// </summary>
[Collection(DiscoveryPostgresCollection.Name)]
public class DetectMutualMatchIntegrationTests(DiscoveryPostgresFixture fixture)
{
    private async Task SeedDogAsync(Guid dogId, Guid ownerId)
    {
        await using var session = fixture.Store.LightweightSession();
        session.Store(new DiscoveryFeedItem { Id = dogId, OwnerId = ownerId, Name = "Test Dog", Breed = "Mixed" });
        await session.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WhenBothDogsHaveLikedEachOther_PublishesMatchCreatedWithBothOwnerIds()
    {
        var dogAId = Guid.NewGuid();
        var dogBId = Guid.NewGuid();
        var ownerAId = Guid.NewGuid();
        var ownerBId = Guid.NewGuid();
        await SeedDogAsync(dogAId, ownerAId);
        await SeedDogAsync(dogBId, ownerBId);

        // Both sides' DogLiked events are already durably in the stream by
        // the time Handle runs, same as production (SwipeOnDogHandler
        // appends and commits before Wolverine's event forwarding invokes
        // this automation) - Handle only uses the trigger event to derive
        // the stream id, its state comes entirely from AggregateStreamAsync.
        var streamId = MatchStream.IdFor(dogAId, dogBId);
        var secondLike = new DogLiked(dogBId, dogAId, DateTimeOffset.UtcNow);
        await using (var seedSession = fixture.Store.LightweightSession())
        {
            seedSession.Events.Append(streamId, new DogLiked(dogAId, dogBId, DateTimeOffset.UtcNow));
            seedSession.Events.Append(streamId, secondLike);
            await seedSession.SaveChangesAsync();
        }

        await using var session = fixture.Store.LightweightSession();
        var result = await DetectMutualMatchHandler.Handle(secondLike, session, CancellationToken.None);

        result.Should().NotBeNull();
        result!.DogAId.Should().Be(dogAId);
        result.DogBId.Should().Be(dogBId);
        new[] { result.OwnerAId, result.OwnerBId }.Should().BeEquivalentTo([ownerAId, ownerBId]);
    }

    [Fact]
    public async Task Handle_WhenOnlyOneSideHasLiked_ReturnsNullAndPublishesNothing()
    {
        var dogAId = Guid.NewGuid();
        var dogBId = Guid.NewGuid();
        await SeedDogAsync(dogAId, Guid.NewGuid());
        await SeedDogAsync(dogBId, Guid.NewGuid());

        await using var session = fixture.Store.LightweightSession();
        var result = await DetectMutualMatchHandler.Handle(
            new DogLiked(dogAId, dogBId, DateTimeOffset.UtcNow), session, CancellationToken.None);

        result.Should().BeNull();
    }
}
