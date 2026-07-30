using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Admin.Api.ReadModels.GetFeedbackDetail;
using K9Crush.Modules.Admin.Domain;
using Xunit;

namespace K9Crush.Modules.Admin.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - GetFeedbackDetailHandler only calls
/// IQuerySession.LoadAsync (no Query&lt;T&gt;() LINQ), so mocks cleanly here.
/// </summary>
public class GetFeedbackDetailHandlerTests
{
    [Fact]
    public async Task Handle_WhenItemDoesNotExist_ReturnsNotFound()
    {
        var feedbackId = Guid.NewGuid();
        var session = Substitute.For<IQuerySession>();
        session.LoadAsync<FeedbackInboxItem>(feedbackId, Arg.Any<CancellationToken>()).Returns((FeedbackInboxItem?)null);

        var result = await GetFeedbackDetailHandler.Handle(feedbackId, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenItemExists_ReturnsDetail()
    {
        var (item, _) = FeedbackInboxItem.CreateNew(Guid.NewGuid(), Guid.NewGuid(), "Great app!", DateTimeOffset.UtcNow);
        item.Respond("Thanks!");
        var session = Substitute.For<IQuerySession>();
        session.LoadAsync<FeedbackInboxItem>(item.Id, Arg.Any<CancellationToken>()).Returns(item);

        var result = await GetFeedbackDetailHandler.Handle(item.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<FeedbackDetailResponse>>();
        var response = ((Ok<FeedbackDetailResponse>)result.Result).Value!;
        response.FeedbackId.Should().Be(item.Id);
        response.Status.Should().Be(nameof(FeedbackStatus.Responded));
        response.ResponseMessage.Should().Be("Thanks!");
    }
}
