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
/// Layer 2 (TestingApproach.md) - AddDogListingPhotoHandler calls
/// FetchForWriting/AppendOne/SaveChangesAsync against DogListing plus a
/// plain LoadAsync against ShelterAccount for the ownership check
/// (ADR-031).
/// </summary>
public class AddDogListingPhotoHandlerTests
{
    private static readonly Guid ShelterOwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    private static (ShelterAccount shelterAccount, DogListing dogListing) SeedListing()
    {
        var shelterAccount = ShelterAccount.RequestNew(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount;
        var dogListing = DogListing.AddNew(shelterAccount.Id, "Biscuit", "Beagle mix", 24, "Friendly").DogListing;
        return (shelterAccount, dogListing);
    }

    private static IDocumentSession BuildSession(ShelterAccount shelterAccount, DogListing? dogListing, out JasperFx.Events.IEventStream<DogListing> stream)
    {
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(dogListing?.Id ?? Guid.NewGuid(), dogListing, out stream);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);
        return session;
    }

    [Fact]
    public async Task Handle_WhenCalledByOwningShelter_AttachesPhotoAndPersists()
    {
        var (shelterAccount, dogListing) = SeedListing();
        var mediaAssetId = Guid.NewGuid();
        var session = BuildSession(shelterAccount, dogListing, out var stream);

        var result = await AddDogListingPhotoHandler.Handle(
            dogListing.Id, new AddDogListingPhotoRequest(mediaAssetId),
            BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<AddDogListingPhotoResponse>>();
        dogListing.PhotoIds.Should().Contain(mediaAssetId);
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.DogListingPhotoAddedV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenListingDoesNotExist_ReturnsNotFound()
    {
        var dogListingId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<DogListing>(dogListingId, null, out _);

        var result = await AddDogListingPhotoHandler.Handle(
            dogListingId, new AddDogListingPhotoRequest(Guid.NewGuid()),
            BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerDoesNotOwnTheShelterAccount_ReturnsForbid()
    {
        var (shelterAccount, dogListing) = SeedListing();
        var session = BuildSession(shelterAccount, dogListing, out _);

        var result = await AddDogListingPhotoHandler.Handle(
            dogListing.Id, new AddDogListingPhotoRequest(Guid.NewGuid()),
            BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }
}
