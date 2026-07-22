using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Places.Api.Commands.WriteReview;
using K9Crush.Modules.Places.Domain;
using Xunit;

namespace K9Crush.Modules.Places.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - WriteReviewHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class WriteReviewHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenPlaceDoesNotExist_ReturnsNotFound()
    {
        var placeId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Place>(placeId, Arg.Any<CancellationToken>()).Returns((Place?)null);

        var result = await WriteReviewHandler.Handle(
            placeId, new WriteReviewRequest(5, "Great walk!", false), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenPlaceExists_CreatesDraftReviewAndPersists()
    {
        var reviewerOwnerId = Guid.NewGuid();
        var place = Place.Create(Guid.NewGuid(), "Bark Park", PlaceType.DogPark);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Place>(place.Id, Arg.Any<CancellationToken>()).Returns(place);

        var result = await WriteReviewHandler.Handle(
            place.Id, new WriteReviewRequest(5, "Great walk!", true), BuildUser(reviewerOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<WriteReviewResponse>>();
        var response = ((Ok<WriteReviewResponse>)result.Result).Value!;
        response.Status.Should().Be(nameof(ReviewStatus.Draft));

        session.Received(1).Store(Arg.Is<Review[]>(arr =>
arr != null &&             arr.Length == 1 && arr[0].PlaceId == place.Id && arr[0].ReviewerOwnerId == reviewerOwnerId && arr[0].Rating == 5));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
