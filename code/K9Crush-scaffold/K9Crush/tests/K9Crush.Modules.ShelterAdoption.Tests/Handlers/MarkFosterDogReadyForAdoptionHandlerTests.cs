using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.MarkFosterDogReadyForAdoption;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - MarkFosterDogReadyForAdoptionHandler
/// only calls FetchForWriting/AppendOne/SaveChangesAsync, so
/// IDocumentSession mocks cleanly here (ADR-031).
/// </summary>
public class MarkFosterDogReadyForAdoptionHandlerTests
{
    private static readonly Guid ShelterAccountId = Guid.NewGuid();

    private static DogListing BuildInFosterListing()
    {
        var dogListing = DogListing.AddNew(ShelterAccountId, "Biscuit", "Beagle mix", 24, "Friendly").DogListing;
        dogListing.PlaceInFoster(Guid.NewGuid());
        return dogListing;
    }

    [Fact]
    public async Task Handle_WhenInFoster_MarksAvailableAndPersists()
    {
        var dogListing = BuildInFosterListing();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(dogListing.Id, dogListing, out var stream);

        var result = await MarkFosterDogReadyForAdoptionHandler.Handle(dogListing.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<MarkFosterDogReadyForAdoptionResponse>>();
        dogListing.Status.Should().Be(DogListingStatus.Available);
        dogListing.CurrentFosterCaregiverOwnerId.Should().NotBeNull();
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.FosterDogMarkedReadyForAdoptionV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenListingDoesNotExist_ReturnsNotFound()
    {
        var dogListingId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<DogListing>(dogListingId, null, out _);

        var result = await MarkFosterDogReadyForAdoptionHandler.Handle(dogListingId, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNotInFoster_ReturnsConflict()
    {
        var dogListing = DogListing.AddNew(ShelterAccountId, "Biscuit", "Beagle mix", 24, "Friendly").DogListing; // NotReadyYet
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(dogListing.Id, dogListing, out _);

        var result = await MarkFosterDogReadyForAdoptionHandler.Handle(dogListing.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
