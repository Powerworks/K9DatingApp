using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Discovery.Api.Commands.BlockMatchAttempt;
using K9Crush.Modules.Discovery.Domain;
using Xunit;

namespace K9Crush.Modules.Discovery.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - BlockMatchAttemptHandler only calls
/// LoadAsync, so IQuerySession mocks cleanly here.
/// </summary>
public class BlockMatchAttemptHandlerTests
{
    [Fact]
    public async Task Handle_WhenDogDoesNotExist_ReturnsNotFound()
    {
        var session = Substitute.For<IQuerySession>();
        var dogId = Guid.NewGuid();
        session.LoadAsync<DiscoveryFeedItem>(dogId, Arg.Any<CancellationToken>()).Returns((DiscoveryFeedItem?)null);

        var result = await BlockMatchAttemptHandler.Handle(dogId, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenDogExists_AlwaysBlocksTheAttempt()
    {
        var dogId = Guid.NewGuid();
        var dog = new DiscoveryFeedItem { Id = dogId, OwnerId = Guid.NewGuid(), Name = "Luna", Breed = "Mixed" };
        var session = Substitute.For<IQuerySession>();
        session.LoadAsync<DiscoveryFeedItem>(dogId, Arg.Any<CancellationToken>()).Returns(dog);

        var result = await BlockMatchAttemptHandler.Handle(dogId, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<BlockMatchAttemptResponse>>();
        var ok = (Ok<BlockMatchAttemptResponse>)result.Result;
        ok.Value!.DogId.Should().Be(dogId);
        ok.Value.Reason.Should().Be("sign_up_required");
    }
}
