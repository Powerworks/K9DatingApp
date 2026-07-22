using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Profiles.Api.Commands.AddDogProfilePhoto;
using K9Crush.Modules.Profiles.Domain;
using Xunit;

namespace K9Crush.Modules.Profiles.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - AddDogProfilePhotoHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly
/// here.
/// </summary>
public class AddDogProfilePhotoHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenDogProfileDoesNotExist_ReturnsNotFound()
    {
        var session = Substitute.For<IDocumentSession>();
        var dogProfileId = Guid.NewGuid();
        session.LoadAsync<DogProfile>(dogProfileId, Arg.Any<CancellationToken>()).Returns((DogProfile?)null);

        var result = await AddDogProfilePhotoHandler.Handle(
            dogProfileId, new AddDogProfilePhotoRequest(Guid.NewGuid()), BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheOwner_ReturnsForbid()
    {
        var dogProfile = DogProfile.Start(OwnerId);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogProfile>(dogProfile.Id, Arg.Any<CancellationToken>()).Returns(dogProfile);

        var result = await AddDogProfilePhotoHandler.Handle(
            dogProfile.Id, new AddDogProfilePhotoRequest(Guid.NewGuid()), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenAlreadyPublished_ReturnsConflict()
    {
        var dogProfile = DogProfile.Start(OwnerId);
        dogProfile.AddDetails("Biscuit", "Labrador", 36, "Friendly", GeoCoordinate.Create(45.5, -122.6));
        dogProfile.AttachPhoto(Guid.NewGuid());
        dogProfile.Publish();

        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogProfile>(dogProfile.Id, Arg.Any<CancellationToken>()).Returns(dogProfile);

        var result = await AddDogProfilePhotoHandler.Handle(
            dogProfile.Id, new AddDogProfilePhotoRequest(Guid.NewGuid()), BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenDraftOwnedByCaller_AttachesPhotoAndPersists()
    {
        var dogProfile = DogProfile.Start(OwnerId);
        var mediaAssetId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogProfile>(dogProfile.Id, Arg.Any<CancellationToken>()).Returns(dogProfile);

        var result = await AddDogProfilePhotoHandler.Handle(
            dogProfile.Id, new AddDogProfilePhotoRequest(mediaAssetId), BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<AddDogProfilePhotoResponse>>();
        dogProfile.PhotoIds.Should().ContainSingle().Which.Should().Be(mediaAssetId);

        session.Received(1).Store(Arg.Is<DogProfile[]>(arr => arr.Length == 1 && arr[0] == dogProfile));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
