using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Admin.Api.Commands.RespondToFeedback;
using K9Crush.Modules.Admin.Domain;
using K9Crush.Modules.Admin.Domain.Events;
using Xunit;

namespace K9Crush.Modules.Admin.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - RespondToFeedbackHandler only calls
/// FetchForWriting/AppendOne/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here (ADR-031).
/// </summary>
public class RespondToFeedbackHandlerTests
{
    [Fact]
    public async Task Handle_WhenItemDoesNotExist_ReturnsNotFound()
    {
        var feedbackId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<FeedbackInboxItem>(feedbackId, null, out _);

        var result = await RespondToFeedbackHandler.Handle(
            feedbackId, new RespondToFeedbackRequest("Thanks!"), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenItemExists_RespondsAndAppendsEvent()
    {
        var (item, _) = FeedbackInboxItem.CreateNew(Guid.NewGuid(), Guid.NewGuid(), "Great app!", DateTimeOffset.UtcNow);
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(item.Id, item, out var stream);

        var result = await RespondToFeedbackHandler.Handle(
            item.Id, new RespondToFeedbackRequest("Thanks for the kind words!"), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<RespondToFeedbackResponse>>();
        var response = ((Ok<RespondToFeedbackResponse>)result.Result).Value!;
        response.Status.Should().Be(nameof(FeedbackStatus.Responded));
        item.ResponseMessage.Should().Be("Thanks for the kind words!");
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && ((FeedbackInboxItemRespondedV1)o).ResponseMessage == "Thanks for the kind words!"));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
