using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Discovery.Api.Commands.FlagDogOfInterest;
using K9Crush.Modules.Discovery.Domain;
using Xunit;

namespace K9Crush.Modules.Discovery.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - FlagDogOfInterestHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly
/// here.
/// </summary>
public class FlagDogOfInterestHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenDogNoLongerAvailable_ReturnsConflict()
    {
        var session = Substitute.For<IDocumentSession>();
        var dogId = Guid.NewGuid();
        session.LoadAsync<DiscoveryFeedItem>(dogId, Arg.Any<CancellationToken>()).Returns((DiscoveryFeedItem?)null);

        var result = await FlagDogOfInterestHandler.Handle(dogId, BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
        ((Conflict<string>)result.Result).Value.Should().Be("Saved Match No Longer Available.");
        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDogExists_FlagsItAndPersists()
    {
        var ownerId = Guid.NewGuid();
        var dogId = Guid.NewGuid();
        var dog = new DiscoveryFeedItem { Id = dogId, OwnerId = Guid.NewGuid(), Name = "Luna", Breed = "Mixed" };
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DiscoveryFeedItem>(dogId, Arg.Any<CancellationToken>()).Returns(dog);

        var result = await FlagDogOfInterestHandler.Handle(dogId, BuildUser(ownerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<FlagDogOfInterestResponse>>();
        ((Ok<FlagDogOfInterestResponse>)result.Result).Value!.DogId.Should().Be(dogId);

        session.Received(1).Store(Arg.Is<DogOfInterest[]>(arr =>
arr != null &&             arr.Length == 1 && arr[0].OwnerId == ownerId && arr[0].DogProfileId == dogId));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
