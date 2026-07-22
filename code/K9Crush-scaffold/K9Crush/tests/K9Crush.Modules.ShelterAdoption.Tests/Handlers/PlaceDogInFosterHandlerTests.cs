using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.PlaceDogInFoster;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - PlaceDogInFosterHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly
/// here.
/// </summary>
public class PlaceDogInFosterHandlerTests
{
    private static readonly Guid ShelterAccountId = Guid.NewGuid();
    private static readonly Guid CaregiverOwnerId = Guid.NewGuid();

    private static DogListing BuildAvailableListing()
    {
        var dogListing = DogListing.Create(ShelterAccountId, "Biscuit", "Beagle mix", 24, "Friendly");
        dogListing.UpdateStatus(DogListingStatus.Available);
        return dogListing;
    }

    private static FosterApplication BuildApprovedFosterApplication()
    {
        var application = FosterApplication.Apply(CaregiverOwnerId, HomeType.House, hasGarden: true, hasOtherPets: false, new DateOnly(2026, 8, 1));
        application.Review();
        application.Approve();
        return application;
    }

    [Fact]
    public async Task Handle_WhenListingIsAvailableAndApplicationIsApproved_PlacesInFosterAndPersists()
    {
        var dogListing = BuildAvailableListing();
        var fosterApplication = BuildApprovedFosterApplication();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogListing>(dogListing.Id, Arg.Any<CancellationToken>()).Returns(dogListing);
        session.LoadAsync<FosterApplication>(fosterApplication.Id, Arg.Any<CancellationToken>()).Returns(fosterApplication);

        var result = await PlaceDogInFosterHandler.Handle(
            dogListing.Id, new PlaceDogInFosterRequest(fosterApplication.Id), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<PlaceDogInFosterResponse>>();
        var response = ((Ok<PlaceDogInFosterResponse>)result.Result).Value!;
        response.Status.Should().Be(nameof(DogListingStatus.InFoster));
        response.FosterCaregiverOwnerId.Should().Be(CaregiverOwnerId);

        session.Received(1).Store(Arg.Is<DogListing[]>(arr =>
            arr != null && arr.Length == 1 && arr[0].Status == DogListingStatus.InFoster &&
            arr[0].CurrentFosterCaregiverOwnerId == CaregiverOwnerId));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenListingDoesNotExist_ReturnsNotFound()
    {
        var session = Substitute.For<IDocumentSession>();
        var dogListingId = Guid.NewGuid();
        session.LoadAsync<DogListing>(dogListingId, Arg.Any<CancellationToken>()).Returns((DogListing?)null);

        var result = await PlaceDogInFosterHandler.Handle(
            dogListingId, new PlaceDogInFosterRequest(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenListingIsAlreadyInFoster_ReturnsConflict()
    {
        var dogListing = BuildAvailableListing();
        dogListing.PlaceInFoster(Guid.NewGuid());
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogListing>(dogListing.Id, Arg.Any<CancellationToken>()).Returns(dogListing);

        var result = await PlaceDogInFosterHandler.Handle(
            dogListing.Id, new PlaceDogInFosterRequest(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenFosterApplicationDoesNotExist_ReturnsNotFound()
    {
        var dogListing = BuildAvailableListing();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogListing>(dogListing.Id, Arg.Any<CancellationToken>()).Returns(dogListing);
        session.LoadAsync<FosterApplication>(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((FosterApplication?)null);

        var result = await PlaceDogInFosterHandler.Handle(
            dogListing.Id, new PlaceDogInFosterRequest(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenFosterApplicationIsNotApproved_ReturnsConflict()
    {
        var dogListing = BuildAvailableListing();
        var fosterApplication = FosterApplication.Apply(CaregiverOwnerId, HomeType.House, hasGarden: true, hasOtherPets: false, new DateOnly(2026, 8, 1)); // Submitted
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogListing>(dogListing.Id, Arg.Any<CancellationToken>()).Returns(dogListing);
        session.LoadAsync<FosterApplication>(fosterApplication.Id, Arg.Any<CancellationToken>()).Returns(fosterApplication);

        var result = await PlaceDogInFosterHandler.Handle(
            dogListing.Id, new PlaceDogInFosterRequest(fosterApplication.Id), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
