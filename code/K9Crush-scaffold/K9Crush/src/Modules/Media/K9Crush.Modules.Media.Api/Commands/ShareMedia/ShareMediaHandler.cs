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

        var mediaAsset = await session.LoadAsync<MediaAsset>(mediaAssetId, cancellationToken);
        if (mediaAsset is null)
            return TypedResults.NotFound();

        if (mediaAsset.OwnerId != callerOwnerId)
            return TypedResults.Forbid();

        mediaAsset.Share(request.Visibility, request.SharedWithOwnerIds ?? []);
        session.Store(mediaAsset);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new ShareMediaResponse(mediaAsset.Id, mediaAsset.Visibility!.Value.ToString(), mediaAsset.SharedAt!.Value));
    }
}
