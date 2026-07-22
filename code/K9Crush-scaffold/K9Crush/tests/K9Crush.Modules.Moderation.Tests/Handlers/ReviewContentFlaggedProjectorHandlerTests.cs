using FluentAssertions;
using Marten;
using NSubstitute;
using K9Crush.Modules.Moderation.Domain;
using K9Crush.Modules.Places.Contracts;
using Xunit;

namespace K9Crush.Modules.Moderation.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ReviewContentFlaggedProjectorHandler
/// only calls Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class ReviewContentFlaggedProjectorHandlerTests
{
    [Fact]
    public async Task Handle_WhenCalled_CreatesAnOpenFlaggedContentEntry()
    {
        var reviewId = Guid.NewGuid();
        var contentOwnerId = Guid.NewGuid();
        var reporterOwnerId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;
        var integrationEvent = new ReviewContentFlaggedV1(
            EventId: Guid.NewGuid(), OccurredAt: occurredAt,
            ReviewId: reviewId, ContentOwnerId: contentOwnerId, ReporterOwnerId: reporterOwnerId);
        var session = Substitute.For<IDocumentSession>();

        await K9Crush.Modules.Moderation.Api.ReadModels.Projectors.ReviewContentFlaggedProjectorHandler.Handle(integrationEvent, session, CancellationToken.None);

        session.Received(1).Store(Arg.Is<FlaggedContent[]>(arr =>
            arr.Length == 1 &&
            arr[0].ContentType == ContentType.Review &&
            arr[0].ContentId == reviewId &&
            arr[0].ContentOwnerId == contentOwnerId &&
            arr[0].ReporterOwnerId == reporterOwnerId &&
            arr[0].FlaggedAt == occurredAt &&
            arr[0].Status == FlaggedContentStatus.Open));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
