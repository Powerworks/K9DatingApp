using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Profiles.Api.Commands.PublishDogProfile;
using K9Crush.Modules.Profiles.Domain;
using Xunit;

namespace K9Crush.Modules.Profiles.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - PublishDogProfileHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly
/// here.
/// </summary>
public class PublishDogProfileHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    private static DogProfile ReadyToPublishDogProfile()
    {
        var dogProfile = DogProfile.Start(OwnerId);
        dogProfile.AddDetails("Biscuit", "Labrador", 36, "Friendly, good with kids", GeoCoordinate.Create(45.5, -122.6));
        dogProfile.AttachPhoto(Guid.NewGuid());
        return dogProfile;
    }

    [Fact]
    public async Task Handle_WhenDogProfileDoesNotExist_ReturnsNotFound()
    {
        var session = Substitute.For<IDocumentSession>();
        var dogProfileId = Guid.NewGuid();
        session.LoadAsync<DogProfile>(dogProfileId, Arg.Any<CancellationToken>()).Returns((DogProfile?)null);

        var (result, integrationEvent) = await PublishDogProfileHandler.Handle(
            dogProfileId, BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheOwner_ReturnsForbid()
    {
        var dogProfile = ReadyToPublishDogProfile();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogProfile>(dogProfile.Id, Arg.Any<CancellationToken>()).Returns(dogProfile);

        var (result, integrationEvent) = await PublishDogProfileHandler.Handle(
            dogProfile.Id, BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenNoPhotoYet_ReturnsPublishBlockedPhotoRequired()
    {
        var dogProfile = DogProfile.Start(OwnerId);
        dogProfile.AddDetails("Biscuit", "Labrador", 36, "Friendly", GeoCoordinate.Create(45.5, -122.6));
        // no AttachPhoto call

        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogProfile>(dogProfile.Id, Arg.Any<CancellationToken>()).Returns(dogProfile);

        var (result, integrationEvent) = await PublishDogProfileHandler.Handle(
            dogProfile.Id, BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
        ((Conflict<string>)result.Result).Value.Should().Be("Publish Blocked: Photo Required.");
        integrationEvent.Should().BeNull();
        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDetailsWereNeverAdded_ReturnsConflict()
    {
        var dogProfile = DogProfile.Start(OwnerId);
        dogProfile.AttachPhoto(Guid.NewGuid());
        // no AddDetails call - Location is still null

        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogProfile>(dogProfile.Id, Arg.Any<CancellationToken>()).Returns(dogProfile);

        var (result, integrationEvent) = await PublishDogProfileHandler.Handle(
            dogProfile.Id, BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenAlreadyPublished_ReturnsConflict()
    {
        var dogProfile = ReadyToPublishDogProfile();
        dogProfile.Publish();

        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogProfile>(dogProfile.Id, Arg.Any<CancellationToken>()).Returns(dogProfile);

        var (result, integrationEvent) = await PublishDogProfileHandler.Handle(
            dogProfile.Id, BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenReadyToPublish_PublishesAndReturnsTheIntegrationEvent()
    {
        var dogProfile = ReadyToPublishDogProfile();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogProfile>(dogProfile.Id, Arg.Any<CancellationToken>()).Returns(dogProfile);

        var (result, integrationEvent) = await PublishDogProfileHandler.Handle(
            dogProfile.Id, BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<PublishDogProfileResponse>>();
        dogProfile.Status.Should().Be(DogProfileStatus.Published);

        integrationEvent.Should().NotBeNull();
        integrationEvent!.DogProfileId.Should().Be(dogProfile.Id);
        integrationEvent.OwnerId.Should().Be(OwnerId);
        integrationEvent.Breed.Should().Be("Labrador");
        integrationEvent.Latitude.Should().Be(45.5);
        integrationEvent.Longitude.Should().Be(-122.6);

        session.Received(1).Store(Arg.Is<DogProfile[]>(arr => arr.Length == 1 && arr[0] == dogProfile));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
