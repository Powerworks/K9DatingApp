using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Media.Api.Commands.RemoveMedia;
using K9Crush.Modules.Media.Domain;
using K9Crush.Modules.Media.Domain.Events;
using Xunit;

namespace K9Crush.Modules.Media.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - RemoveMediaHandler only calls
/// FetchForWriting/AppendOne/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here (ADR-031).
/// </summary>
public class RemoveMediaHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenAssetDoesNotExist_ReturnsNotFound()
    {
        var mediaAssetId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<MediaAsset>(mediaAssetId, null, out _);

        var result = await RemoveMediaHandler.Handle(
            mediaAssetId, new RemoveMediaRequest(false), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenAssetAlreadyRemoved_ReturnsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var (asset, _) = MediaAsset.Upload(ownerId, MediaType.Photo, "https://storage.example/photo.jpg");
        asset.Remove();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(asset.Id, asset, out _);

        var result = await RemoveMediaHandler.Handle(
            asset.Id, new RemoveMediaRequest(false), BuildUser(ownerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheOwner_ReturnsForbid()
    {
        var ownerId = Guid.NewGuid();
        var (asset, _) = MediaAsset.Upload(ownerId, MediaType.Photo, "https://storage.example/photo.jpg");
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(asset.Id, asset, out _);

        var result = await RemoveMediaHandler.Handle(
            asset.Id, new RemoveMediaRequest(false), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsTheOwner_AppendsRemovedEvent()
    {
        var ownerId = Guid.NewGuid();
        var (asset, _) = MediaAsset.Upload(ownerId, MediaType.Photo, "https://storage.example/photo.jpg");
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(asset.Id, asset, out var stream);

        var result = await RemoveMediaHandler.Handle(
            asset.Id, new RemoveMediaRequest(true), BuildUser(ownerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok>();
        asset.IsRemoved.Should().BeTrue();
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && ((MediaAssetRemovedV1)o).OccurredAt <= DateTimeOffset.UtcNow));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
