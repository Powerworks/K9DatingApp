using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.ReadModels.GetDogListingDetails;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - GetDogListingDetailsHandler only calls
/// IQuerySession.LoadAsync (no Query&lt;T&gt;() LINQ), so mocks cleanly here.
/// </summary>
public class GetDogListingDetailsHandlerTests
{
    [Fact]
    public async Task Handle_WhenDogListingDoesNotExist_ReturnsNotFound()
    {
        var dogListingId = Guid.NewGuid();
        var session = Substitute.For<IQuerySession>();
        session.LoadAsync<DogListing>(dogListingId, Arg.Any<CancellationToken>()).Returns((DogListing?)null);

        var result = await GetDogListingDetailsHandler.Handle(dogListingId, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenDogListingExists_ReturnsItsDetails()
    {
        var dogListing = DogListing.Create(Guid.NewGuid(), "Biscuit", "Labrador", 36, "Friendly");
        var session = Substitute.For<IQuerySession>();
        session.LoadAsync<DogListing>(dogListing.Id, Arg.Any<CancellationToken>()).Returns(dogListing);

        var result = await GetDogListingDetailsHandler.Handle(dogListing.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<DogListingDetailsResponse>>();
        var response = ((Ok<DogListingDetailsResponse>)result.Result).Value!;
        response.DogListingId.Should().Be(dogListing.Id);
        response.Name.Should().Be("Biscuit");
        response.Breed.Should().Be("Labrador");
        response.AgeInMonths.Should().Be(36);
        response.Bio.Should().Be("Friendly");
        response.ShelterAccountId.Should().Be(dogListing.ShelterAccountId);
    }
}
