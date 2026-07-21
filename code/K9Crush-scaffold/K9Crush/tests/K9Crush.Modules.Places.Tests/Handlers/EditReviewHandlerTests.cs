using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Places.Api.Commands.EditReview;
using K9Crush.Modules.Places.Domain;
using Xunit;

namespace K9Crush.Modules.Places.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - EditReviewHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class EditReviewHandlerTests
{
    private static readonly Guid ReviewerOwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenReviewDoesNotExist_ReturnsNotFound()
    {
        var reviewId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Review>(reviewId, Arg.Any<CancellationToken>()).Returns((Review?)null);

        var result = await EditReviewHandler.Handle(reviewId, new EditReviewRequest(3, "Meh"), BuildUser(ReviewerOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheReviewer_ReturnsForbid()
    {
        var review = Review.Write(Guid.NewGuid(), ReviewerOwnerId, 5, "Great walk!", false);
        review.Publish();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Review>(review.Id, Arg.Any<CancellationToken>()).Returns(review);

        var result = await EditReviewHandler.Handle(review.Id, new EditReviewRequest(3, "Meh"), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenNotPublished_ReturnsConflict()
    {
        var review = Review.Write(Guid.NewGuid(), ReviewerOwnerId, 5, "Great walk!", false); // Draft, not Published
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Review>(review.Id, Arg.Any<CancellationToken>()).Returns(review);

        var result = await EditReviewHandler.Handle(review.Id, new EditReviewRequest(3, "Meh"), BuildUser(ReviewerOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenPublishedAndCallerIsReviewer_EditsAndPersists()
    {
        var review = Review.Write(Guid.NewGuid(), ReviewerOwnerId, 5, "Great walk!", false);
        review.Publish();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Review>(review.Id, Arg.Any<CancellationToken>()).Returns(review);

        var result = await EditReviewHandler.Handle(review.Id, new EditReviewRequest(3, "Actually just okay."), BuildUser(ReviewerOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<EditReviewResponse>>();
        review.Rating.Should().Be(3);
        review.Body.Should().Be("Actually just okay.");
        session.Received(1).Store(Arg.Is<Review[]>(arr => arr.Length == 1 && arr[0] == review));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
