using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Admin.Api.Commands.ResolveFeedback;
using K9Crush.Modules.Admin.Domain;
using K9Crush.Modules.Admin.Domain.Events;
using Xunit;

namespace K9Crush.Modules.Admin.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ResolveFeedbackHandler only calls
/// FetchForWriting/AppendOne/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here (ADR-031).
/// </summary>
public class ResolveFeedbackHandlerTests
{
    [Fact]
    public async Task Handle_WhenItemDoesNotExist_ReturnsNotFound()
    {
        var feedbackId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<FeedbackInboxItem>(feedbackId, null, out _);

        var result = await ResolveFeedbackHandler.Handle(feedbackId, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenItemHasNotBeenRespondedToYet_ReturnsConflict()
    {
        var (item, _) = FeedbackInboxItem.CreateNew(Guid.NewGuid(), Guid.NewGuid(), "Great app!", DateTimeOffset.UtcNow);
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(item.Id, item, out _);

        var result = await ResolveFeedbackHandler.Handle(item.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenItemHasBeenRespondedTo_ResolvesAndAppendsEvent()
    {
        var (item, _) = FeedbackInboxItem.CreateNew(Guid.NewGuid(), Guid.NewGuid(), "Great app!", DateTimeOffset.UtcNow);
        item.Respond("Thanks!");
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(item.Id, item, out var stream);

        var result = await ResolveFeedbackHandler.Handle(item.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ResolveFeedbackResponse>>();
        item.Status.Should().Be(FeedbackStatus.Resolved);
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && ((FeedbackInboxItemResolvedV1)o).ResolvedAt <= DateTimeOffset.UtcNow));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
