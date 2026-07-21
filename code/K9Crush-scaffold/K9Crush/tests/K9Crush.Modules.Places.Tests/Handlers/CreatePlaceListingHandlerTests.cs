using System.Security.Claims;
using FluentAssertions;
using Marten;
using NSubstitute;
using K9Crush.Modules.Places.Api.Commands.CreatePlaceListing;
using K9Crush.Modules.Places.Domain;
using Xunit;

namespace K9Crush.Modules.Places.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - CreatePlaceListingHandler only calls
/// Store/SaveChangesAsync (no LoadAsync - Place is always newly created),
/// so IDocumentSession mocks cleanly here.
/// </summary>
public class CreatePlaceListingHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenCalled_CreatesPlaceOwnedByCallerAndPersists()
    {
        var ownerId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();

        var response = await CreatePlaceListingHandler.Handle(
            new CreatePlaceListingRequest("Bark Park", PlaceType.DogPark), BuildUser(ownerId), session, CancellationToken.None);

        response.PlaceId.Should().NotBeEmpty();
        session.Received(1).Store(Arg.Is<Place[]>(arr =>
            arr.Length == 1 && arr[0].OwnerId == ownerId && arr[0].Name == "Bark Park" && arr[0].PlaceType == PlaceType.DogPark));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
