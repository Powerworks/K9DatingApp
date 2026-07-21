using FluentAssertions;
using Marten;
using NSubstitute;
using K9Crush.Modules.Media.Api.Automations.RemoveMediaOnContentRemovalRequested;
using K9Crush.Modules.Media.Domain;
using K9Crush.Modules.Moderation.Contracts;
using Xunit;

namespace K9Crush.Modules.Media.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - RemoveMediaOnContentRemovalRequestedHandler
/// only calls LoadAsync/Delete/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here.
/// </summary>
public class RemoveMediaOnContentRemovalRequestedHandlerTests
{
    private static ContentRemovalRequestedV1 BuildEvent(string contentType, Guid contentId) => new(
        EventId: Guid.NewGuid(), OccurredAt: DateTimeOffset.UtcNow, FlagId: Guid.NewGuid(), ContentType: contentType, ContentId: contentId);

    [Fact]
    public async Task Handle_WhenContentTypeIsNotMedia_DoesNothing()
    {
        var session = Substitute.For<IDocumentSession>();

        await RemoveMediaOnContentRemovalRequestedHandler.Handle(BuildEvent("Message", Guid.NewGuid()), session, CancellationToken.None);

        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_WhenMediaAssetDoesNotExist_DoesNothing()
    {
        var mediaAssetId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<MediaAsset>(mediaAssetId, Arg.Any<CancellationToken>()).Returns((MediaAsset?)null);

        await RemoveMediaOnContentRemovalRequestedHandler.Handle(BuildEvent("Media", mediaAssetId), session, CancellationToken.None);

        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_WhenContentTypeIsMediaAndAssetExists_DeletesIt()
    {
        var asset = MediaAsset.Upload(Guid.NewGuid(), MediaType.Photo, "https://storage.example/photo.jpg");
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<MediaAsset>(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        await RemoveMediaOnContentRemovalRequestedHandler.Handle(BuildEvent("Media", asset.Id), session, CancellationToken.None);

        session.Received(1).Delete(asset);
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
