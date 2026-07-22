using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.EndFosterPlacement;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - EndFosterPlacementHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly
/// here.
/// </summary>
public class EndFosterPlacementHandlerTests
{
    private static readonly Guid ShelterAccountId = Guid.NewGuid();

    private static DogListing BuildInFosterListing()
    {
        var dogListing = DogListing.Create(ShelterAccountId, "Biscuit", "Beagle mix", 24, "Friendly");
        dogListing.PlaceInFoster(Guid.NewGuid());
        return dogListing;
    }

    [Fact]
    public async Task Handle_WhenPlacementIsActive_EndsItAndPersists()
    {
        var dogListing = BuildInFosterListing();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogListing>(dogListing.Id, Arg.Any<CancellationToken>()).Returns(dogListing);

        var result = await EndFosterPlacementHandler.Handle(
            dogListing.Id, new EndFosterPlacementRequest(FosterPlacementEndReason.ReturnedToShelter), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<EndFosterPlacementResponse>>();
        session.Received(1).Store(Arg.Is<DogListing[]>(arr =>
            arr != null && arr.Length == 1 && arr[0].Status == DogListingStatus.Available &&
            arr[0].CurrentFosterCaregiverOwnerId == null));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenListingDoesNotExist_ReturnsNotFound()
    {
        var session = Substitute.For<IDocumentSession>();
        var dogListingId = Guid.NewGuid();
        session.LoadAsync<DogListing>(dogListingId, Arg.Any<CancellationToken>()).Returns((DogListing?)null);

        var result = await EndFosterPlacementHandler.Handle(
            dogListingId, new EndFosterPlacementRequest(FosterPlacementEndReason.ReturnedToShelter), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNoActivePlacement_ReturnsConflict()
    {
        var dogListing = DogListing.Create(ShelterAccountId, "Biscuit", "Beagle mix", 24, "Friendly");
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogListing>(dogListing.Id, Arg.Any<CancellationToken>()).Returns(dogListing);

        var result = await EndFosterPlacementHandler.Handle(
            dogListing.Id, new EndFosterPlacementRequest(FosterPlacementEndReason.ReturnedToShelter), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
