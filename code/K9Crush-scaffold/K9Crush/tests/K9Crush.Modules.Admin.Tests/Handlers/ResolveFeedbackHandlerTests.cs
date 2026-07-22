using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Admin.Api.Commands.ResolveFeedback;
using K9Crush.Modules.Admin.Domain;
using Xunit;

namespace K9Crush.Modules.Admin.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ResolveFeedbackHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class ResolveFeedbackHandlerTests
{
    [Fact]
    public async Task Handle_WhenItemDoesNotExist_ReturnsNotFound()
    {
        var feedbackId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<FeedbackInboxItem>(feedbackId, Arg.Any<CancellationToken>()).Returns((FeedbackInboxItem?)null);

        var result = await ResolveFeedbackHandler.Handle(feedbackId, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenItemHasNotBeenRespondedToYet_ReturnsConflict()
    {
        var item = FeedbackInboxItem.Create(Guid.NewGuid(), Guid.NewGuid(), "Great app!", DateTimeOffset.UtcNow);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<FeedbackInboxItem>(item.Id, Arg.Any<CancellationToken>()).Returns(item);

        var result = await ResolveFeedbackHandler.Handle(item.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenItemHasBeenRespondedTo_ResolvesAndPersists()
    {
        var item = FeedbackInboxItem.Create(Guid.NewGuid(), Guid.NewGuid(), "Great app!", DateTimeOffset.UtcNow);
        item.Respond("Thanks!");
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<FeedbackInboxItem>(item.Id, Arg.Any<CancellationToken>()).Returns(item);

        var result = await ResolveFeedbackHandler.Handle(item.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ResolveFeedbackResponse>>();
        item.Status.Should().Be(FeedbackStatus.Resolved);
        session.Received(1).Store(Arg.Is<FeedbackInboxItem[]>(arr => arr != null && arr.Length == 1 && arr[0] == item));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
