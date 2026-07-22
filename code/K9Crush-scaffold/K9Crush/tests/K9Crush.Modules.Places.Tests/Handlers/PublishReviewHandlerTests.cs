using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Places.Api.Commands.PublishReview;
using K9Crush.Modules.Places.Domain;
using Xunit;

namespace K9Crush.Modules.Places.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - PublishReviewHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class PublishReviewHandlerTests
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

        var result = await PublishReviewHandler.Handle(reviewId, BuildUser(ReviewerOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheReviewer_ReturnsForbid()
    {
        var review = Review.Write(Guid.NewGuid(), ReviewerOwnerId, 5, "Great walk!", false);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Review>(review.Id, Arg.Any<CancellationToken>()).Returns(review);

        var result = await PublishReviewHandler.Handle(review.Id, BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenNotInDraftStatus_ReturnsConflict()
    {
        var review = Review.Write(Guid.NewGuid(), ReviewerOwnerId, 5, "Great walk!", false);
        review.Publish(); // already Published, not Draft
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Review>(review.Id, Arg.Any<CancellationToken>()).Returns(review);

        var result = await PublishReviewHandler.Handle(review.Id, BuildUser(ReviewerOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenDraftAndCallerIsReviewer_PublishesAndPersists()
    {
        var review = Review.Write(Guid.NewGuid(), ReviewerOwnerId, 5, "Great walk!", false);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Review>(review.Id, Arg.Any<CancellationToken>()).Returns(review);

        var result = await PublishReviewHandler.Handle(review.Id, BuildUser(ReviewerOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<PublishReviewResponse>>();
        review.Status.Should().Be(ReviewStatus.Published);
        session.Received(1).Store(Arg.Is<Review[]>(arr => arr != null && arr.Length == 1 && arr[0] == review));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
