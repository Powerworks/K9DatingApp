using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Places.Api.Commands.RespondToReview;
using K9Crush.Modules.Places.Domain;
using Xunit;

namespace K9Crush.Modules.Places.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - RespondToReviewHandler only calls
/// LoadAsync (twice, two different document types)/Store/SaveChangesAsync,
/// so IDocumentSession mocks cleanly here.
/// </summary>
public class RespondToReviewHandlerTests
{
    private static readonly Guid ReviewerOwnerId = Guid.NewGuid();
    private static readonly Guid PlaceOwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenReviewDoesNotExist_ReturnsNotFound()
    {
        var reviewId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Review>(reviewId, Arg.Any<CancellationToken>()).Returns((Review?)null);

        var result = await RespondToReviewHandler.Handle(
            reviewId, new RespondToReviewRequest("Thanks!", ResponderRole.ParkOwner), BuildUser(PlaceOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerDoesNotOwnThePlace_ReturnsForbid()
    {
        var place = Place.Create(PlaceOwnerId, "Bark Park", PlaceType.DogPark);
        var review = Review.Write(place.Id, ReviewerOwnerId, 5, "Great walk!", false);
        review.Publish();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Review>(review.Id, Arg.Any<CancellationToken>()).Returns(review);
        session.LoadAsync<Place>(place.Id, Arg.Any<CancellationToken>()).Returns(place);

        var result = await RespondToReviewHandler.Handle(
            review.Id, new RespondToReviewRequest("Thanks!", ResponderRole.ParkOwner), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenReviewIsNotPublished_ReturnsConflict()
    {
        var place = Place.Create(PlaceOwnerId, "Bark Park", PlaceType.DogPark);
        var review = Review.Write(place.Id, ReviewerOwnerId, 5, "Great walk!", false); // Draft, not Published
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Review>(review.Id, Arg.Any<CancellationToken>()).Returns(review);
        session.LoadAsync<Place>(place.Id, Arg.Any<CancellationToken>()).Returns(place);

        var result = await RespondToReviewHandler.Handle(
            review.Id, new RespondToReviewRequest("Thanks!", ResponderRole.ParkOwner), BuildUser(PlaceOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenPublishedAndCallerOwnsThePlace_RespondsAndPersists()
    {
        var place = Place.Create(PlaceOwnerId, "Bark Park", PlaceType.DogPark);
        var review = Review.Write(place.Id, ReviewerOwnerId, 5, "Great walk!", false);
        review.Publish();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Review>(review.Id, Arg.Any<CancellationToken>()).Returns(review);
        session.LoadAsync<Place>(place.Id, Arg.Any<CancellationToken>()).Returns(place);

        var result = await RespondToReviewHandler.Handle(
            review.Id, new RespondToReviewRequest("Thanks for visiting!", ResponderRole.ParkOwner), BuildUser(PlaceOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<RespondToReviewResponse>>();
        review.ResponseText.Should().Be("Thanks for visiting!");
        review.ResponderRole.Should().Be(ResponderRole.ParkOwner);
        session.Received(1).Store(Arg.Is<Review[]>(arr => arr != null && arr.Length == 1 && arr[0] == review));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
