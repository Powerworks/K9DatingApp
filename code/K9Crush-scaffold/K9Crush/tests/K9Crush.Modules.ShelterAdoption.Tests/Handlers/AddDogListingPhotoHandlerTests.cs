using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.AddDogListingPhoto;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - AddDogListingPhotoHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly
/// here.
/// </summary>
public class AddDogListingPhotoHandlerTests
{
    private static readonly Guid ShelterOwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    private static (ShelterAccount shelterAccount, DogListing dogListing) SeedListing()
    {
        var shelterAccount = ShelterAccount.Create(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
        var dogListing = DogListing.Create(shelterAccount.Id, "Biscuit", "Beagle mix", 24, "Friendly");
        return (shelterAccount, dogListing);
    }

    [Fact]
    public async Task Handle_WhenCalledByOwningShelter_AttachesPhotoAndPersists()
    {
        var (shelterAccount, dogListing) = SeedListing();
        var mediaAssetId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogListing>(dogListing.Id, Arg.Any<CancellationToken>()).Returns(dogListing);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await AddDogListingPhotoHandler.Handle(
            dogListing.Id, new AddDogListingPhotoRequest(mediaAssetId),
            BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<AddDogListingPhotoResponse>>();
        session.Received(1).Store(Arg.Is<DogListing[]>(arr =>
            arr != null && arr.Length == 1 && arr[0].PhotoIds.Contains(mediaAssetId)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenListingDoesNotExist_ReturnsNotFound()
    {
        var session = Substitute.For<IDocumentSession>();
        var dogListingId = Guid.NewGuid();
        session.LoadAsync<DogListing>(dogListingId, Arg.Any<CancellationToken>()).Returns((DogListing?)null);

        var result = await AddDogListingPhotoHandler.Handle(
            dogListingId, new AddDogListingPhotoRequest(Guid.NewGuid()),
            BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerDoesNotOwnTheShelterAccount_ReturnsForbid()
    {
        var (shelterAccount, dogListing) = SeedListing();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogListing>(dogListing.Id, Arg.Any<CancellationToken>()).Returns(dogListing);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await AddDogListingPhotoHandler.Handle(
            dogListing.Id, new AddDogListingPhotoRequest(Guid.NewGuid()),
            BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }
}
