using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.EndFosterPlacement;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - EndFosterPlacementHandler only calls
/// FetchForWriting/AppendOne/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here (ADR-031).
/// </summary>
public class EndFosterPlacementHandlerTests
{
    private static readonly Guid ShelterAccountId = Guid.NewGuid();

    private static DogListing BuildInFosterListing()
    {
        var dogListing = DogListing.AddNew(ShelterAccountId, "Biscuit", "Beagle mix", 24, "Friendly").DogListing;
        dogListing.PlaceInFoster(Guid.NewGuid());
        return dogListing;
    }

    [Fact]
    public async Task Handle_WhenPlacementIsActive_EndsItAndPersists()
    {
        var dogListing = BuildInFosterListing();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(dogListing.Id, dogListing, out var stream);

        var result = await EndFosterPlacementHandler.Handle(
            dogListing.Id, new EndFosterPlacementRequest(FosterPlacementEndReason.ReturnedToShelter), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<EndFosterPlacementResponse>>();
        dogListing.Status.Should().Be(DogListingStatus.Available);
        dogListing.CurrentFosterCaregiverOwnerId.Should().BeNull();
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.FosterPlacementEndedV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenListingDoesNotExist_ReturnsNotFound()
    {
        var dogListingId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<DogListing>(dogListingId, null, out _);

        var result = await EndFosterPlacementHandler.Handle(
            dogListingId, new EndFosterPlacementRequest(FosterPlacementEndReason.ReturnedToShelter), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNoActivePlacement_ReturnsConflict()
    {
        var dogListing = DogListing.AddNew(ShelterAccountId, "Biscuit", "Beagle mix", 24, "Friendly").DogListing;
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(dogListing.Id, dogListing, out _);

        var result = await EndFosterPlacementHandler.Handle(
            dogListing.Id, new EndFosterPlacementRequest(FosterPlacementEndReason.ReturnedToShelter), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
