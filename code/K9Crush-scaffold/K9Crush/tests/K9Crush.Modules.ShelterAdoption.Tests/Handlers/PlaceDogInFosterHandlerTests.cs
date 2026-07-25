using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.PlaceDogInFoster;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - PlaceDogInFosterHandler calls
/// FetchForWriting/AppendOne/SaveChangesAsync against DogListing plus a
/// plain LoadAsync against FosterApplication (read-only reference check,
/// not a self-load - ADR-031).
/// </summary>
public class PlaceDogInFosterHandlerTests
{
    private static readonly Guid ShelterAccountId = Guid.NewGuid();
    private static readonly Guid CaregiverOwnerId = Guid.NewGuid();

    private static DogListing BuildAvailableListing()
    {
        var dogListing = DogListing.AddNew(ShelterAccountId, "Biscuit", "Beagle mix", 24, "Friendly").DogListing;
        dogListing.UpdateStatus(DogListingStatus.Available);
        return dogListing;
    }

    private static FosterApplication BuildApprovedFosterApplication()
    {
        var application = FosterApplication.ApplyNew(CaregiverOwnerId, HomeType.House, hasGarden: true, hasOtherPets: false, new DateOnly(2026, 8, 1)).FosterApplication;
        application.Review();
        application.Approve();
        return application;
    }

    [Fact]
    public async Task Handle_WhenListingIsAvailableAndApplicationIsApproved_PlacesInFosterAndPersists()
    {
        var dogListing = BuildAvailableListing();
        var fosterApplication = BuildApprovedFosterApplication();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(dogListing.Id, dogListing, out var stream);
        session.LoadAsync<FosterApplication>(fosterApplication.Id, Arg.Any<CancellationToken>()).Returns(fosterApplication);

        var result = await PlaceDogInFosterHandler.Handle(
            dogListing.Id, new PlaceDogInFosterRequest(fosterApplication.Id), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<PlaceDogInFosterResponse>>();
        var response = ((Ok<PlaceDogInFosterResponse>)result.Result).Value!;
        response.Status.Should().Be(nameof(DogListingStatus.InFoster));
        response.FosterCaregiverOwnerId.Should().Be(CaregiverOwnerId);

        dogListing.Status.Should().Be(DogListingStatus.InFoster);
        dogListing.CurrentFosterCaregiverOwnerId.Should().Be(CaregiverOwnerId);
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.DogListingPlacedInFosterV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenListingDoesNotExist_ReturnsNotFound()
    {
        var dogListingId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<DogListing>(dogListingId, null, out _);

        var result = await PlaceDogInFosterHandler.Handle(
            dogListingId, new PlaceDogInFosterRequest(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenListingIsAlreadyInFoster_ReturnsConflict()
    {
        var dogListing = BuildAvailableListing();
        dogListing.PlaceInFoster(Guid.NewGuid());
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(dogListing.Id, dogListing, out _);

        var result = await PlaceDogInFosterHandler.Handle(
            dogListing.Id, new PlaceDogInFosterRequest(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenFosterApplicationDoesNotExist_ReturnsNotFound()
    {
        var dogListing = BuildAvailableListing();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(dogListing.Id, dogListing, out _);
        session.LoadAsync<FosterApplication>(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((FosterApplication?)null);

        var result = await PlaceDogInFosterHandler.Handle(
            dogListing.Id, new PlaceDogInFosterRequest(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenFosterApplicationIsNotApproved_ReturnsConflict()
    {
        var dogListing = BuildAvailableListing();
        var fosterApplication = FosterApplication.ApplyNew(CaregiverOwnerId, HomeType.House, hasGarden: true, hasOtherPets: false, new DateOnly(2026, 8, 1)).FosterApplication; // Submitted
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(dogListing.Id, dogListing, out _);
        session.LoadAsync<FosterApplication>(fosterApplication.Id, Arg.Any<CancellationToken>()).Returns(fosterApplication);

        var result = await PlaceDogInFosterHandler.Handle(
            dogListing.Id, new PlaceDogInFosterRequest(fosterApplication.Id), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
