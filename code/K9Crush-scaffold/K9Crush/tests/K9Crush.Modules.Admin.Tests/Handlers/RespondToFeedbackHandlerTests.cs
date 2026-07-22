using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Admin.Api.Commands.RespondToFeedback;
using K9Crush.Modules.Admin.Domain;
using Xunit;

namespace K9Crush.Modules.Admin.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - RespondToFeedbackHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class RespondToFeedbackHandlerTests
{
    [Fact]
    public async Task Handle_WhenItemDoesNotExist_ReturnsNotFound()
    {
        var feedbackId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<FeedbackInboxItem>(feedbackId, Arg.Any<CancellationToken>()).Returns((FeedbackInboxItem?)null);

        var result = await RespondToFeedbackHandler.Handle(
            feedbackId, new RespondToFeedbackRequest("Thanks!"), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenItemExists_RespondsAndPersists()
    {
        var item = FeedbackInboxItem.Create(Guid.NewGuid(), Guid.NewGuid(), "Great app!", DateTimeOffset.UtcNow);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<FeedbackInboxItem>(item.Id, Arg.Any<CancellationToken>()).Returns(item);

        var result = await RespondToFeedbackHandler.Handle(
            item.Id, new RespondToFeedbackRequest("Thanks for the kind words!"), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<RespondToFeedbackResponse>>();
        var response = ((Ok<RespondToFeedbackResponse>)result.Result).Value!;
        response.Status.Should().Be(nameof(FeedbackStatus.Responded));
        item.ResponseMessage.Should().Be("Thanks for the kind words!");
        session.Received(1).Store(Arg.Is<FeedbackInboxItem[]>(arr => arr != null && arr.Length == 1 && arr[0] == item));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
