using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Profiles.Api.ReadModels.GetDogProfile;
using K9Crush.Modules.Profiles.Domain;
using Xunit;

namespace K9Crush.Modules.Profiles.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - GetDogProfileHandler only calls
/// IQuerySession.LoadAsync (no Query&lt;T&gt;() LINQ), so mocks cleanly here.
/// </summary>
public class GetDogProfileHandlerTests
{
    [Fact]
    public async Task Handle_WhenDogProfileDoesNotExist_ReturnsNotFound()
    {
        var dogProfileId = Guid.NewGuid();
        var session = Substitute.For<IQuerySession>();
        session.LoadAsync<DogProfile>(dogProfileId, Arg.Any<CancellationToken>()).Returns((DogProfile?)null);

        var result = await GetDogProfileHandler.Handle(dogProfileId, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenDogProfileExists_ReturnsItsDetails()
    {
        var dogProfile = DogProfile.Start(Guid.NewGuid());
        dogProfile.AddDetails("Biscuit", "Labrador", 36, "Friendly", GeoCoordinate.Create(45.5, -122.6));
        var photoId = Guid.NewGuid();
        dogProfile.AttachPhoto(photoId);

        var session = Substitute.For<IQuerySession>();
        session.LoadAsync<DogProfile>(dogProfile.Id, Arg.Any<CancellationToken>()).Returns(dogProfile);

        var result = await GetDogProfileHandler.Handle(dogProfile.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<DogProfileResponse>>();
        var response = ((Ok<DogProfileResponse>)result.Result).Value!;
        response.DogProfileId.Should().Be(dogProfile.Id);
        response.Status.Should().Be(dogProfile.Status.ToString());
        response.Name.Should().Be("Biscuit");
        response.Breed.Should().Be("Labrador");
        response.AgeInMonths.Should().Be(36);
        response.Bio.Should().Be("Friendly");
        response.PhotoIds.Should().ContainSingle().Which.Should().Be(photoId);
    }
}
