using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Profiles.Api.Commands.AddDogProfileDetails;
using K9Crush.Modules.Profiles.Domain;
using Xunit;

namespace K9Crush.Modules.Profiles.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - AddDogProfileDetailsHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly
/// here.
/// </summary>
public class AddDogProfileDetailsHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    private static AddDogProfileDetailsRequest ValidRequest() =>
        new("Biscuit", "Labrador", 36, "Friendly, good with kids", 45.5, -122.6);

    [Fact]
    public async Task Handle_WhenDogProfileDoesNotExist_ReturnsNotFound()
    {
        var session = Substitute.For<IDocumentSession>();
        var dogProfileId = Guid.NewGuid();
        session.LoadAsync<DogProfile>(dogProfileId, Arg.Any<CancellationToken>()).Returns((DogProfile?)null);

        var result = await AddDogProfileDetailsHandler.Handle(
            dogProfileId, ValidRequest(), BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheOwner_ReturnsForbid()
    {
        var dogProfile = DogProfile.Start(OwnerId);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogProfile>(dogProfile.Id, Arg.Any<CancellationToken>()).Returns(dogProfile);

        var result = await AddDogProfileDetailsHandler.Handle(
            dogProfile.Id, ValidRequest(), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

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

        var result = await AddDogProfileDetailsHandler.Handle(
            dogProfile.Id, ValidRequest(), BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenDraftOwnedByCaller_AddsDetailsAndPersists()
    {
        var dogProfile = DogProfile.Start(OwnerId);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogProfile>(dogProfile.Id, Arg.Any<CancellationToken>()).Returns(dogProfile);

        var result = await AddDogProfileDetailsHandler.Handle(
            dogProfile.Id, ValidRequest(), BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<AddDogProfileDetailsResponse>>();
        dogProfile.Name.Should().Be("Biscuit");
        dogProfile.Breed.Should().Be("Labrador");
        dogProfile.AgeInMonths.Should().Be(36);
        dogProfile.Location.Should().Be(GeoCoordinate.Create(45.5, -122.6));

        session.Received(1).Store(Arg.Is<DogProfile[]>(arr => arr != null && arr.Length == 1 && arr[0] == dogProfile));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
