using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Places.Api.Commands.ReportReview;
using K9Crush.Modules.Places.Domain;
using Xunit;

namespace K9Crush.Modules.Places.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ReportReviewHandler only calls LoadAsync
/// (no Store/SaveChangesAsync - reporting doesn't mutate the review
/// itself), so IDocumentSession mocks cleanly here.
/// </summary>
public class ReportReviewHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenReviewDoesNotExist_ReturnsNotFoundAndCascadesNothing()
    {
        var reviewId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Review>(reviewId, Arg.Any<CancellationToken>()).Returns((Review?)null);

        var (result, integrationEvent) = await ReportReviewHandler.Handle(reviewId, BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenReviewExists_CascadesContentFlaggedForAnyReporterRegardlessOfOwnership()
    {
        var reporterId = Guid.NewGuid();
        var reviewerOwnerId = Guid.NewGuid();
        var review = Review.Write(Guid.NewGuid(), reviewerOwnerId, 1, "Terrible!", false); // reporter is NOT the reviewer

        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Review>(review.Id, Arg.Any<CancellationToken>()).Returns(review);

        var (result, integrationEvent) = await ReportReviewHandler.Handle(review.Id, BuildUser(reporterId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ReportReviewResponse>>();
        integrationEvent.Should().NotBeNull();
        integrationEvent!.ReviewId.Should().Be(review.Id);
        integrationEvent.ContentOwnerId.Should().Be(reviewerOwnerId);
        integrationEvent.ReporterOwnerId.Should().Be(reporterId);
    }
}
