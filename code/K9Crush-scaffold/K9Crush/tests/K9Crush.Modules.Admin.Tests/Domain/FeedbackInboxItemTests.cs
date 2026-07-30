using FluentAssertions;
using K9Crush.Modules.Admin.Domain;
using Xunit;

namespace K9Crush.Modules.Admin.Tests.Domain;

/// <summary>
/// Layer 1 (TestingApproach.md) - pure unit tests of FeedbackInboxItem's
/// factory method and domain methods. No mocks, no infra. Deliberately
/// does NOT test calling Resolve() before Respond() - this codebase's
/// domain methods don't guard their own preconditions (see Application.cs's
/// own doc comments); that guarantee belongs to the handler tests instead.
/// </summary>
public class FeedbackInboxItemTests
{
    [Fact]
    public void CreateNew_WhenCalled_CreatesOpenItemWithMatchingIdAndReturnsTheEvent()
    {
        var feedbackId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var submittedAt = DateTimeOffset.UtcNow;

        var (item, @event) = FeedbackInboxItem.CreateNew(feedbackId, ownerId, "Great app!", submittedAt);

        item.Id.Should().Be(feedbackId);
        item.OwnerId.Should().Be(ownerId);
        item.Message.Should().Be("Great app!");
        item.SubmittedAt.Should().Be(submittedAt);
        item.Status.Should().Be(FeedbackStatus.Open);
        item.ResponseMessage.Should().BeNull();
        item.RespondedAt.Should().BeNull();
        item.ResolvedAt.Should().BeNull();

        @event.FeedbackId.Should().Be(feedbackId);
        @event.OwnerId.Should().Be(ownerId);
    }

    [Fact]
    public void Respond_WhenCalled_SetsResponseMessageAndRespondedAtAndMovesToResponded()
    {
        var (item, _) = FeedbackInboxItem.CreateNew(Guid.NewGuid(), Guid.NewGuid(), "Great app!", DateTimeOffset.UtcNow);
        var before = DateTimeOffset.UtcNow;

        var @event = item.Respond("  Thanks for the kind words!  ");

        var after = DateTimeOffset.UtcNow;
        item.ResponseMessage.Should().Be("Thanks for the kind words!");
        item.RespondedAt.Should().NotBeNull();
        item.RespondedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        item.Status.Should().Be(FeedbackStatus.Responded);

        @event.ResponseMessage.Should().Be("Thanks for the kind words!");
    }

    [Fact]
    public void Resolve_WhenCalled_SetsResolvedAtAndMovesToResolved()
    {
        var (item, _) = FeedbackInboxItem.CreateNew(Guid.NewGuid(), Guid.NewGuid(), "Great app!", DateTimeOffset.UtcNow);
        item.Respond("Thanks!");
        var before = DateTimeOffset.UtcNow;

        item.Resolve();

        var after = DateTimeOffset.UtcNow;
        item.ResolvedAt.Should().NotBeNull();
        item.ResolvedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        item.Status.Should().Be(FeedbackStatus.Resolved);
    }
}
