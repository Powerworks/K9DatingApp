using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Discovery.Api.Commands.UndoLastSwipe;
using K9Crush.Modules.Discovery.Domain;
using K9Crush.Modules.Discovery.Domain.Events;
using Xunit;

namespace K9Crush.IntegrationTests.Discovery;

/// <summary>
/// Layer 3 (TestingApproach.md) - covers UndoLastSwipeHandler's branches that
/// need AggregateStreamAsync against a real event store: reversing an
/// existing swipe, and rejecting when there's nothing to undo. See
/// UndoLastSwipeHandlerTests (Layer 2) for the NotFound/Forbid branches,
/// which only need LoadAsync and mock cleanly.
/// </summary>
[Collection(DiscoveryPostgresCollection.Name)]
public class UndoLastSwipeIntegrationTests(DiscoveryPostgresFixture fixture)
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    private async Task SeedSwiperDogAsync(Guid dogId, Guid ownerId)
    {
        await using var session = fixture.Store.LightweightSession();
        session.Store(new DiscoveryFeedItem { Id = dogId, OwnerId = ownerId, Breed = "Mixed" });
        await session.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WhenAnActiveSwipeExists_AppendsSwipeUndoneAndReturnsOk()
    {
        var swiperDogId = Guid.NewGuid();
        var targetDogId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        await SeedSwiperDogAsync(swiperDogId, ownerId);

        var streamId = MatchStream.IdFor(swiperDogId, targetDogId);
        await using (var seedSession = fixture.Store.LightweightSession())
        {
            seedSession.Events.Append(streamId, new DogLiked(swiperDogId, targetDogId, DateTimeOffset.UtcNow));
            await seedSession.SaveChangesAsync();
        }

        await using var session = fixture.Store.LightweightSession();
        var result = await UndoLastSwipeHandler.Handle(
            new UndoLastSwipeRequest(swiperDogId, targetDogId),
            BuildUser(ownerId),
            session,
            CancellationToken.None);

        result.Result.Should().BeOfType<Ok<UndoLastSwipeResponse>>();
        ((Ok<UndoLastSwipeResponse>)result.Result).Value!.Acknowledged.Should().BeTrue();

        await using var verifySession = fixture.Store.LightweightSession();
        var state = await verifySession.Events.AggregateStreamAsync<UndoLastSwipeState>(streamId);
        state!.HasActiveSwipeFor(swiperDogId).Should().BeFalse("the swipe was just undone");
    }

    [Fact]
    public async Task Handle_WhenThereIsNoSwipeToUndo_ReturnsConflict()
    {
        var swiperDogId = Guid.NewGuid();
        var targetDogId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        await SeedSwiperDogAsync(swiperDogId, ownerId);

        await using var session = fixture.Store.LightweightSession();
        var result = await UndoLastSwipeHandler.Handle(
            new UndoLastSwipeRequest(swiperDogId, targetDogId),
            BuildUser(ownerId),
            session,
            CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenTheSwipeWasAlreadyUndone_ReturnsConflictOnASecondAttempt()
    {
        var swiperDogId = Guid.NewGuid();
        var targetDogId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        await SeedSwiperDogAsync(swiperDogId, ownerId);

        var streamId = MatchStream.IdFor(swiperDogId, targetDogId);
        await using (var seedSession = fixture.Store.LightweightSession())
        {
            seedSession.Events.Append(streamId, new DogLiked(swiperDogId, targetDogId, DateTimeOffset.UtcNow));
            await seedSession.SaveChangesAsync();
        }

        await using (var firstUndoSession = fixture.Store.LightweightSession())
        {
            await UndoLastSwipeHandler.Handle(
                new UndoLastSwipeRequest(swiperDogId, targetDogId), BuildUser(ownerId), firstUndoSession, CancellationToken.None);
        }

        await using var secondUndoSession = fixture.Store.LightweightSession();
        var result = await UndoLastSwipeHandler.Handle(
            new UndoLastSwipeRequest(swiperDogId, targetDogId), BuildUser(ownerId), secondUndoSession, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
