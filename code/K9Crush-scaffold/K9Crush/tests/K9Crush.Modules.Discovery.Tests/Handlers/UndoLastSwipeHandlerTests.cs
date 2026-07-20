using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Discovery.Api.Commands.UndoLastSwipe;
using K9Crush.Modules.Discovery.Domain;
using Xunit;

namespace K9Crush.Modules.Discovery.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - covers the two branches UndoLastSwipeHandler
/// can resolve without needing AggregateStreamAsync (NotFound/Forbid), which
/// only touch LoadAsync so IDocumentSession mocks cleanly here. The
/// "no swipe to undo" / "reverses an existing swipe" branches need a real
/// event store (AggregateStreamAsync can't be meaningfully mocked) - see
/// UndoLastSwipeIntegrationTests (Layer 3) for those.
/// </summary>
public class UndoLastSwipeHandlerTests
{
    private static readonly Guid SwiperDogId = Guid.NewGuid();
    private static readonly Guid TargetDogId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenSwiperDogDoesNotExist_ReturnsNotFound()
    {
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DiscoveryFeedItem>(SwiperDogId, Arg.Any<CancellationToken>()).Returns((DiscoveryFeedItem?)null);

        var result = await UndoLastSwipeHandler.Handle(
            new UndoLastSwipeRequest(SwiperDogId, TargetDogId),
            BuildUser(OwnerId),
            session,
            CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerDoesNotOwnSwiperDog_ReturnsForbid()
    {
        var swiperDog = new DiscoveryFeedItem { Id = SwiperDogId, OwnerId = Guid.NewGuid(), Breed = "Mixed" };
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DiscoveryFeedItem>(SwiperDogId, Arg.Any<CancellationToken>()).Returns(swiperDog);

        var result = await UndoLastSwipeHandler.Handle(
            new UndoLastSwipeRequest(SwiperDogId, TargetDogId),
            BuildUser(OwnerId), // does not match swiperDog.OwnerId
            session,
            CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }
}
