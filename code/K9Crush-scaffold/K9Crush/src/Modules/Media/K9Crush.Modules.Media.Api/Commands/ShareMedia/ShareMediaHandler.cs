using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Media.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Media.Api.Commands.ShareMedia;

/// <summary>
/// State-change slice: the emlang yaml's UploadShareRemovePhotosAndVideos
/// chapter's "Share Media" -> "Media Shared". Ownership-gated - only the
/// uploader can share their own media.
///
/// ADR-031: FetchForWriting replaces LoadAsync/Store - fetches the current
/// aggregate and stages the append in one call, with optimistic-concurrency
/// checked at SaveChangesAsync.
/// </summary>
public static class ShareMediaHandler
{
    [WolverinePost("/api/v1/media/{mediaAssetId:guid}/share")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<ShareMediaResponse>, NotFound, ForbidHttpResult>> Handle(
        Guid mediaAssetId,
        ShareMediaRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var stream = await session.Events.FetchForWriting<MediaAsset>(mediaAssetId, cancellationToken);
        var mediaAsset = stream.Aggregate;
        if (mediaAsset is null || mediaAsset.IsRemoved)
            return TypedResults.NotFound();

        if (mediaAsset.OwnerId != callerOwnerId)
            return TypedResults.Forbid();

        var @event = mediaAsset.Share(request.Visibility, request.SharedWithOwnerIds ?? []);
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new ShareMediaResponse(mediaAsset.Id, mediaAsset.Visibility!.Value.ToString(), mediaAsset.SharedAt!.Value));
    }
}
