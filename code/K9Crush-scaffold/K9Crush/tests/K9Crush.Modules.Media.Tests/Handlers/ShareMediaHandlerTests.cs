using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Media.Api.Commands.ShareMedia;
using K9Crush.Modules.Media.Domain;
using Xunit;

namespace K9Crush.Modules.Media.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ShareMediaHandler only calls LoadAsync/
/// Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class ShareMediaHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenAssetDoesNotExist_ReturnsNotFound()
    {
        var mediaAssetId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<MediaAsset>(mediaAssetId, Arg.Any<CancellationToken>()).Returns((MediaAsset?)null);

        var result = await ShareMediaHandler.Handle(
            mediaAssetId, new ShareMediaRequest(MediaVisibility.Public, null), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheOwner_ReturnsForbid()
    {
        var ownerId = Guid.NewGuid();
        var asset = MediaAsset.Upload(ownerId, MediaType.Photo, "https://storage.example/photo.jpg");
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<MediaAsset>(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        var result = await ShareMediaHandler.Handle(
            asset.Id, new ShareMediaRequest(MediaVisibility.Public, null), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsTheOwner_SharesAndPersists()
    {
        var ownerId = Guid.NewGuid();
        var asset = MediaAsset.Upload(ownerId, MediaType.Photo, "https://storage.example/photo.jpg");
        var sharedWith = new[] { Guid.NewGuid() };
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<MediaAsset>(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        var result = await ShareMediaHandler.Handle(
            asset.Id, new ShareMediaRequest(MediaVisibility.SpecificPeople, sharedWith), BuildUser(ownerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ShareMediaResponse>>();
        ((Ok<ShareMediaResponse>)result.Result).Value!.Visibility.Should().Be(nameof(MediaVisibility.SpecificPeople));
        asset.SharedWithOwnerIds.Should().BeEquivalentTo(sharedWith);
        session.Received(1).Store(Arg.Is<MediaAsset[]>(arr => arr != null && arr.Length == 1 && arr[0] == asset));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
