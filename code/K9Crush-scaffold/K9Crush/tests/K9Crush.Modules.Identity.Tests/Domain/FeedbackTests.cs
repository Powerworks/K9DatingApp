using FluentAssertions;
using K9Crush.Modules.Identity.Domain;
using Xunit;

namespace K9Crush.Modules.Identity.Tests.Domain;

/// <summary>Layer 1 (TestingApproach.md) - pure unit test of Feedback's factory method.</summary>
public class FeedbackTests
{
    [Fact]
    public void Submit_WhenCalled_CreatesFeedbackWithTrimmedMessageAndSubmittedAt()
    {
        var ownerId = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow;

        var (feedback, _) = Feedback.Submit(ownerId, "  This app is great!  ");

        var after = DateTimeOffset.UtcNow;
        feedback.OwnerId.Should().Be(ownerId);
        feedback.Message.Should().Be("This app is great!");
        feedback.SubmittedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Submit_WhenMessageIsBlank_Throws(string message)
    {
        var act = () => Feedback.Submit(Guid.NewGuid(), message);

        act.Should().Throw<ArgumentException>();
    }
}
