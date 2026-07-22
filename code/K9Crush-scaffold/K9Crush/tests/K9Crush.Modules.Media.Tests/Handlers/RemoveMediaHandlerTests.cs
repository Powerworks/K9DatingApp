using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Media.Api.Commands.RemoveMedia;
using K9Crush.Modules.Media.Domain;
using Xunit;

namespace K9Crush.Modules.Media.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - RemoveMediaHandler only calls LoadAsync/
/// Delete/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class RemoveMediaHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenAssetDoesNotExist_ReturnsNotFound()
    {
        var mediaAssetId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<MediaAsset>(mediaAssetId, Arg.Any<CancellationToken>()).Returns((MediaAsset?)null);

        var result = await RemoveMediaHandler.Handle(
            mediaAssetId, new RemoveMediaRequest(false), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheOwner_ReturnsForbid()
    {
        var ownerId = Guid.NewGuid();
        var asset = MediaAsset.Upload(ownerId, MediaType.Photo, "https://storage.example/photo.jpg");
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<MediaAsset>(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        var result = await RemoveMediaHandler.Handle(
            asset.Id, new RemoveMediaRequest(false), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsTheOwner_DeletesAndPersists()
    {
        var ownerId = Guid.NewGuid();
        var asset = MediaAsset.Upload(ownerId, MediaType.Photo, "https://storage.example/photo.jpg");
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<MediaAsset>(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        var result = await RemoveMediaHandler.Handle(
            asset.Id, new RemoveMediaRequest(true), BuildUser(ownerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok>();
        session.Received(1).Delete(asset);
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
