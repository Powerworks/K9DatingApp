using System.Security.Claims;
using FluentAssertions;
using Marten;
using Marten.Events;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Discovery.Api.Commands.SwipeOnDog;
using K9Crush.Modules.Discovery.Domain;
using K9Crush.Modules.Discovery.Domain.Events;
using Xunit;

namespace K9Crush.Modules.Discovery.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - SwipeOnDogHandler never queries or
/// aggregates a stream (unlike UndoLastSwipeHandler's AggregateStreamAsync
/// path) - it only calls LoadAsync, then blindly appends one event via
/// session.Events.Append(...) and SaveChangesAsync. session.Events
/// (IEventStoreOperations) mocks cleanly for a plain Append(...) call,
/// same as IDocumentSession's other members - it's Query&lt;T&gt;()/
/// AggregateStreamAsync specifically that Layer 2 mocks can't reach, not
/// every event-store member.
/// </summary>
public class SwipeOnDogHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid SwiperDogId = Guid.NewGuid();
    private static readonly Guid TargetDogId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenSwiperDogDoesNotExist_ReturnsNotFound()
    {
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DiscoveryFeedItem>(SwiperDogId, Arg.Any<CancellationToken>()).Returns((DiscoveryFeedItem?)null);

        var result = await SwipeOnDogHandler.Handle(
            new SwipeOnDogRequest(SwiperDogId, TargetDogId, Liked: true), BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerDoesNotOwnSwiperDog_ReturnsForbid()
    {
        var swiperDog = new DiscoveryFeedItem { Id = SwiperDogId, OwnerId = Guid.NewGuid(), Breed = "Mixed" };
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DiscoveryFeedItem>(SwiperDogId, Arg.Any<CancellationToken>()).Returns(swiperDog);

        var result = await SwipeOnDogHandler.Handle(
            new SwipeOnDogRequest(SwiperDogId, TargetDogId, Liked: true), BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenLikedIsTrue_AppendsDogLikedToThePairStream()
    {
        var swiperDog = new DiscoveryFeedItem { Id = SwiperDogId, OwnerId = OwnerId, Breed = "Mixed" };
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DiscoveryFeedItem>(SwiperDogId, Arg.Any<CancellationToken>()).Returns(swiperDog);
        var eventStore = Substitute.For<IEventStoreOperations>();
        session.Events.Returns(eventStore);

        var result = await SwipeOnDogHandler.Handle(
            new SwipeOnDogRequest(SwiperDogId, TargetDogId, Liked: true), BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<SwipeOnDogResponse>>();
        ((Ok<SwipeOnDogResponse>)result.Result).Value!.Acknowledged.Should().BeTrue();

        var expectedStreamId = MatchStream.IdFor(SwiperDogId, TargetDogId);
        eventStore.Received(1).Append(expectedStreamId, Arg.Is<object[]>(events => IsSingleDogLiked(events, SwiperDogId, TargetDogId)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenLikedIsFalse_AppendsDogPassedToThePairStream()
    {
        var swiperDog = new DiscoveryFeedItem { Id = SwiperDogId, OwnerId = OwnerId, Breed = "Mixed" };
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DiscoveryFeedItem>(SwiperDogId, Arg.Any<CancellationToken>()).Returns(swiperDog);
        var eventStore = Substitute.For<IEventStoreOperations>();
        session.Events.Returns(eventStore);

        var result = await SwipeOnDogHandler.Handle(
            new SwipeOnDogRequest(SwiperDogId, TargetDogId, Liked: false), BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<SwipeOnDogResponse>>();

        var expectedStreamId = MatchStream.IdFor(SwiperDogId, TargetDogId);
        eventStore.Received(1).Append(expectedStreamId, Arg.Is<object[]>(events => events.Length == 1 && events[0].GetType() == typeof(DogPassed)));
    }

    private static bool IsSingleDogLiked(object[] events, Guid swiperDogId, Guid targetDogId) =>
        events.Length == 1 &&
        events[0] is DogLiked liked &&
        liked.SwiperDogId == swiperDogId &&
        liked.TargetDogId == targetDogId;
}
