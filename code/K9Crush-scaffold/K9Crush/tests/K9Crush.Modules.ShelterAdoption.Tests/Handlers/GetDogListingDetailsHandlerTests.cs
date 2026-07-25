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
/// IQuerySession.LoadAsync against DogListing's Inline snapshot (no
/// Query&lt;T&gt;() LINQ), so mocks cleanly here (ADR-031).
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
        var dogListing = DogListing.AddNew(Guid.NewGuid(), "Biscuit", "Labrador", 36, "Friendly").DogListing;
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
        response.Status.Should().Be(DogListingStatus.NotReadyYet);
    }

    [Fact]
    public async Task Handle_WhenDogListingWasRemoved_ReturnsNotFound()
    {
        var dogListing = DogListing.AddNew(Guid.NewGuid(), "Biscuit", "Labrador", 36, "Friendly").DogListing;
        dogListing.Remove();
        var session = Substitute.For<IQuerySession>();
        session.LoadAsync<DogListing>(dogListing.Id, Arg.Any<CancellationToken>()).Returns(dogListing);

        var result = await GetDogListingDetailsHandler.Handle(dogListing.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>("a withdrawn listing must not be visible via this endpoint under the no-hard-delete pattern");
    }
}
