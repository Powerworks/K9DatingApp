using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Media.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Media.Api.Commands.RemoveMedia;

/// <summary>
/// State-change slice: the emlang yaml's UploadShareRemovePhotosAndVideos
/// chapter's "Remove Media" -> "Media Removed" - a genuine document
/// delete (same reasoning as RemoveDogListingHandler - nothing reads a
/// removed asset, no history needed). Ownership-gated - only the
/// uploader can remove their own media. See RemoveMediaRequest's doc
/// comment for why CascadeDeletesEngagement is accepted but unused.
/// </summary>
public static class RemoveMediaHandler
{
    [WolverinePost("/api/v1/media/{mediaAssetId:guid}/remove")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok, NotFound, ForbidHttpResult>> Handle(
        Guid mediaAssetId,
        RemoveMediaRequest request,
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

        session.Delete(mediaAsset);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok();
    }
}
