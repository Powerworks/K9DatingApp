using FluentAssertions;
using Marten;
using NSubstitute;
using K9Crush.Modules.Admin.Api.ReadModels.Projectors;
using K9Crush.Modules.Admin.Domain;
using K9Crush.Modules.Identity.Contracts;
using Xunit;

namespace K9Crush.Modules.Admin.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - FeedbackSubmittedProjectorHandler only
/// calls Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class FeedbackSubmittedProjectorHandlerTests
{
    [Fact]
    public async Task Handle_WhenCalled_StoresAnOpenFeedbackInboxItemKeyedByFeedbackId()
    {
        var feedbackId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var submittedAt = DateTimeOffset.UtcNow;
        var integrationEvent = new FeedbackSubmittedV1(
            EventId: Guid.NewGuid(), OccurredAt: DateTimeOffset.UtcNow,
            FeedbackId: feedbackId, OwnerId: ownerId, Message: "Great app!", SubmittedAt: submittedAt);
        var session = Substitute.For<IDocumentSession>();

        await FeedbackSubmittedProjectorHandler.Handle(integrationEvent, session, CancellationToken.None);

        session.Received(1).Store(Arg.Is<FeedbackInboxItem[]>(arr =>
            arr.Length == 1 &&
            arr[0].Id == feedbackId &&
            arr[0].OwnerId == ownerId &&
            arr[0].Message == "Great app!" &&
            arr[0].SubmittedAt == submittedAt &&
            arr[0].Status == FeedbackStatus.Open));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
