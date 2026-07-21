using FluentAssertions;
using K9Crush.Modules.Places.Domain;
using Xunit;

namespace K9Crush.Modules.Places.Tests.Domain;

/// <summary>
/// Layer 1 (TestingApproach.md) - pure unit tests of Review's factory
/// method and domain methods. No mocks, no infra. Deliberately does NOT
/// test calling a method from the "wrong" status (e.g. Edit() on a
/// Draft) - this codebase's domain methods don't guard their own
/// preconditions (see Application.cs's own doc comments); that guarantee
/// belongs to the handler tests instead.
/// </summary>
public class ReviewTests
{
    private static readonly Guid PlaceId = Guid.NewGuid();
    private static readonly Guid ReviewerOwnerId = Guid.NewGuid();

    [Fact]
    public void Write_WhenCalled_CreatesDraftReview()
    {
        var review = Review.Write(PlaceId, ReviewerOwnerId, 5, "  Great walk!  ", visitVerificationRequired: true);

        review.PlaceId.Should().Be(PlaceId);
        review.ReviewerOwnerId.Should().Be(ReviewerOwnerId);
        review.Rating.Should().Be(5);
        review.Body.Should().Be("Great walk!");
        review.VisitVerificationRequired.Should().BeTrue();
        review.Status.Should().Be(ReviewStatus.Draft);
        review.ResponseText.Should().BeNull();
        review.ResponderRole.Should().BeNull();
        review.RespondedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Write_WhenBodyIsBlank_Throws(string body)
    {
        var act = () => Review.Write(PlaceId, ReviewerOwnerId, 5, body, false);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Publish_WhenCalled_MovesToPublished()
    {
        var review = Review.Write(PlaceId, ReviewerOwnerId, 5, "Great walk!", false);

        review.Publish();

        review.Status.Should().Be(ReviewStatus.Published);
    }

    [Fact]
    public void Edit_WhenCalled_UpdatesRatingAndBody()
    {
        var review = Review.Write(PlaceId, ReviewerOwnerId, 5, "Great walk!", false);
        review.Publish();

        review.Edit(3, "  Actually just okay.  ");

        review.Rating.Should().Be(3);
        review.Body.Should().Be("Actually just okay.");
    }

    [Fact]
    public void Remove_WhenCalled_MovesToRemoved()
    {
        var review = Review.Write(PlaceId, ReviewerOwnerId, 5, "Great walk!", false);
        review.Publish();

        review.Remove();

        review.Status.Should().Be(ReviewStatus.Removed);
    }

    [Fact]
    public void Respond_WhenCalled_SetsResponseTextResponderRoleAndRespondedAt()
    {
        var review = Review.Write(PlaceId, ReviewerOwnerId, 5, "Great walk!", false);
        review.Publish();
        var before = DateTimeOffset.UtcNow;

        review.Respond("  Thanks for visiting!  ", ResponderRole.ParkOwner);

        var after = DateTimeOffset.UtcNow;
        review.ResponseText.Should().Be("Thanks for visiting!");
        review.ResponderRole.Should().Be(ResponderRole.ParkOwner);
        review.RespondedAt.Should().NotBeNull();
        review.RespondedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}
