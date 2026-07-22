using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.MarkFosterDogReadyForAdoption;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - MarkFosterDogReadyForAdoptionHandler
/// only calls LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here.
/// </summary>
public class MarkFosterDogReadyForAdoptionHandlerTests
{
    private static readonly Guid ShelterAccountId = Guid.NewGuid();

    private static DogListing BuildInFosterListing()
    {
        var dogListing = DogListing.Create(ShelterAccountId, "Biscuit", "Beagle mix", 24, "Friendly");
        dogListing.PlaceInFoster(Guid.NewGuid());
        return dogListing;
    }

    [Fact]
    public async Task Handle_WhenInFoster_MarksAvailableAndPersists()
    {
        var dogListing = BuildInFosterListing();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogListing>(dogListing.Id, Arg.Any<CancellationToken>()).Returns(dogListing);

        var result = await MarkFosterDogReadyForAdoptionHandler.Handle(dogListing.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<MarkFosterDogReadyForAdoptionResponse>>();
        session.Received(1).Store(Arg.Is<DogListing[]>(arr =>
            arr != null && arr.Length == 1 && arr[0].Status == DogListingStatus.Available &&
            arr[0].CurrentFosterCaregiverOwnerId != null));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenListingDoesNotExist_ReturnsNotFound()
    {
        var session = Substitute.For<IDocumentSession>();
        var dogListingId = Guid.NewGuid();
        session.LoadAsync<DogListing>(dogListingId, Arg.Any<CancellationToken>()).Returns((DogListing?)null);

        var result = await MarkFosterDogReadyForAdoptionHandler.Handle(dogListingId, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNotInFoster_ReturnsConflict()
    {
        var dogListing = DogListing.Create(ShelterAccountId, "Biscuit", "Beagle mix", 24, "Friendly"); // NotReadyYet
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogListing>(dogListing.Id, Arg.Any<CancellationToken>()).Returns(dogListing);

        var result = await MarkFosterDogReadyForAdoptionHandler.Handle(dogListing.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
