using FluentAssertions;
using Marten;
using NSubstitute;
using K9Crush.Modules.Media.Contracts;
using K9Crush.Modules.Moderation.Api.ReadModels.Projectors;
using K9Crush.Modules.Moderation.Domain;
using Xunit;

namespace K9Crush.Modules.Moderation.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - MediaContentFlaggedProjectorHandler only
/// calls Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class MediaContentFlaggedProjectorHandlerTests
{
    [Fact]
    public async Task Handle_WhenCalled_CreatesAnOpenFlaggedContentEntry()
    {
        var mediaAssetId = Guid.NewGuid();
        var contentOwnerId = Guid.NewGuid();
        var reporterOwnerId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;
        var integrationEvent = new MediaContentFlaggedV1(
            EventId: Guid.NewGuid(), OccurredAt: occurredAt,
            MediaAssetId: mediaAssetId, ContentOwnerId: contentOwnerId, ReporterOwnerId: reporterOwnerId);
        var session = Substitute.For<IDocumentSession>();

        await MediaContentFlaggedProjectorHandler.Handle(integrationEvent, session, CancellationToken.None);

        session.Received(1).Store(Arg.Is<FlaggedContent[]>(arr =>
            arr.Length == 1 &&
            arr[0].ContentType == ContentType.Media &&
            arr[0].ContentId == mediaAssetId &&
            arr[0].ContentOwnerId == contentOwnerId &&
            arr[0].ReporterOwnerId == reporterOwnerId &&
            arr[0].FlaggedAt == occurredAt &&
            arr[0].Status == FlaggedContentStatus.Open));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
