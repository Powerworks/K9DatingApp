using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Media.Api.Commands.ShareMedia;
using K9Crush.Modules.Media.Domain;
using K9Crush.Modules.Media.Domain.Events;
using Xunit;

namespace K9Crush.Modules.Media.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ShareMediaHandler only calls
/// FetchForWriting/AppendOne/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here (ADR-031: FetchForWriting is a genuine IEventStoreOperations
/// interface member, confirmed via reflection, not an unmockable extension
/// method).
/// </summary>
public class ShareMediaHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenAssetDoesNotExist_ReturnsNotFound()
    {
        var mediaAssetId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<MediaAsset>(mediaAssetId, null, out _);

        var result = await ShareMediaHandler.Handle(
            mediaAssetId, new ShareMediaRequest(MediaVisibility.Public, null), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenAssetIsRemoved_ReturnsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var (asset, _) = MediaAsset.Upload(ownerId, MediaType.Photo, "https://storage.example/photo.jpg");
        asset.Remove();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(asset.Id, asset, out _);

        var result = await ShareMediaHandler.Handle(
            asset.Id, new ShareMediaRequest(MediaVisibility.Public, null), BuildUser(ownerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheOwner_ReturnsForbid()
    {
        var ownerId = Guid.NewGuid();
        var (asset, _) = MediaAsset.Upload(ownerId, MediaType.Photo, "https://storage.example/photo.jpg");
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(asset.Id, asset, out _);

        var result = await ShareMediaHandler.Handle(
            asset.Id, new ShareMediaRequest(MediaVisibility.Public, null), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsTheOwner_SharesAndAppendsEvent()
    {
        var ownerId = Guid.NewGuid();
        var (asset, _) = MediaAsset.Upload(ownerId, MediaType.Photo, "https://storage.example/photo.jpg");
        var sharedWith = new[] { Guid.NewGuid() };
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(asset.Id, asset, out var stream);

        var result = await ShareMediaHandler.Handle(
            asset.Id, new ShareMediaRequest(MediaVisibility.SpecificPeople, sharedWith), BuildUser(ownerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ShareMediaResponse>>();
        ((Ok<ShareMediaResponse>)result.Result).Value!.Visibility.Should().Be(nameof(MediaVisibility.SpecificPeople));
        asset.SharedWithOwnerIds.Should().BeEquivalentTo(sharedWith);
        // AppendOne(object) - confirmed via reflection against the installed
        // Marten 9.17.1 that IEventStream<T>.AppendOne takes a plain object,
        // not a generic parameter. Plain cast, not `is` pattern-matching -
        // Arg.Is's predicate is an Expression<Predicate<T>>, and expression
        // trees can't contain `is` patterns (CS8122).
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && ((MediaAssetSharedV1)o).Visibility == MediaVisibility.SpecificPeople));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
