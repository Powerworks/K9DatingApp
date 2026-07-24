using FluentAssertions;
using Marten;
using NSubstitute;
using K9Crush.Modules.Admin.Api.ReadModels.Projectors;
using K9Crush.Modules.Admin.Domain;
using K9Crush.Modules.Admin.Domain.Events;
using K9Crush.Modules.Identity.Contracts;
using Xunit;

namespace K9Crush.Modules.Admin.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - FeedbackSubmittedProjectorHandler only
/// calls Events.AggregateStreamAsync/Events.StartStream/SaveChangesAsync,
/// so IDocumentSession mocks cleanly here (ADR-031).
/// </summary>
public class FeedbackSubmittedProjectorHandlerTests
{
    private static IDocumentSession BuildSessionWithExistingStream(Guid feedbackId, FeedbackInboxItem? existing)
    {
        var session = Substitute.For<IDocumentSession>();
        var eventStore = Substitute.For<Marten.Events.IEventStoreOperations>();
        session.Events.Returns(eventStore);
        eventStore.AggregateStreamAsync<FeedbackInboxItem>(
                Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<DateTimeOffset?>(), Arg.Any<FeedbackInboxItem>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(Task.FromResult(existing));
        return session;
    }

    [Fact]
    public async Task Handle_WhenNoStreamExistsYet_StartsANewOpenFeedbackInboxItemStream()
    {
        var feedbackId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var submittedAt = DateTimeOffset.UtcNow;
        var integrationEvent = new FeedbackSubmittedV1(
            EventId: Guid.NewGuid(), OccurredAt: DateTimeOffset.UtcNow,
            FeedbackId: feedbackId, OwnerId: ownerId, Message: "Great app!", SubmittedAt: submittedAt);
        var session = BuildSessionWithExistingStream(feedbackId, null);

        await FeedbackSubmittedProjectorHandler.Handle(integrationEvent, session, CancellationToken.None);

        session.Events.Received(1).StartStream<FeedbackInboxItem>(
            feedbackId,
            Arg.Is<object[]>(events => events != null && events.Length == 1 && events[0] != null
                && ((FeedbackInboxItemCreatedV1)events[0]).OwnerId == ownerId
                && ((FeedbackInboxItemCreatedV1)events[0]).Message == "Great app!"));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenStreamAlreadyExists_IsANoOp()
    {
        var feedbackId = Guid.NewGuid();
        var (existing, _) = FeedbackInboxItem.CreateNew(feedbackId, Guid.NewGuid(), "Great app!", DateTimeOffset.UtcNow);
        var integrationEvent = new FeedbackSubmittedV1(
            EventId: Guid.NewGuid(), OccurredAt: DateTimeOffset.UtcNow,
            FeedbackId: feedbackId, OwnerId: existing.OwnerId, Message: "Great app!", SubmittedAt: existing.SubmittedAt);
        var session = BuildSessionWithExistingStream(feedbackId, existing);

        await FeedbackSubmittedProjectorHandler.Handle(integrationEvent, session, CancellationToken.None);

        session.Events.DidNotReceiveWithAnyArgs().StartStream<FeedbackInboxItem>(default, Array.Empty<object>());
        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
