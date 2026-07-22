using FluentAssertions;
using K9Crush.Modules.Moderation.Domain;
using Xunit;

namespace K9Crush.Modules.Moderation.Tests.Domain;

/// <summary>Layer 1 (TestingApproach.md) - pure unit tests of FlaggedContent's factory method and domain methods. No mocks, no infra.</summary>
public class FlaggedContentTests
{
    [Fact]
    public void Create_WhenCalled_CreatesOpenFlag()
    {
        var contentId = Guid.NewGuid();
        var contentOwnerId = Guid.NewGuid();
        var reporterOwnerId = Guid.NewGuid();
        var flaggedAt = DateTimeOffset.UtcNow;

        var flag = FlaggedContent.Create(ContentType.Media, contentId, contentOwnerId, reporterOwnerId, flaggedAt);

        flag.ContentType.Should().Be(ContentType.Media);
        flag.ContentId.Should().Be(contentId);
        flag.ContentOwnerId.Should().Be(contentOwnerId);
        flag.ReporterOwnerId.Should().Be(reporterOwnerId);
        flag.FlaggedAt.Should().Be(flaggedAt);
        flag.Status.Should().Be(FlaggedContentStatus.Open);
    }

    [Fact]
    public void Dismiss_WhenCalled_MovesToDismissed()
    {
        var flag = FlaggedContent.Create(ContentType.Media, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        flag.Dismiss();

        flag.Status.Should().Be(FlaggedContentStatus.Dismissed);
    }

    [Fact]
    public void MarkContentRemoved_WhenCalled_MovesToContentRemoved()
    {
        var flag = FlaggedContent.Create(ContentType.Media, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        flag.MarkContentRemoved();

        flag.Status.Should().Be(FlaggedContentStatus.ContentRemoved);
    }
}
