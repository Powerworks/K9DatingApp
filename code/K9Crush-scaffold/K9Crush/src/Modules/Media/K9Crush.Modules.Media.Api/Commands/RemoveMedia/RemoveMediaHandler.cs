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
/// chapter's "Remove Media" -> "Media Removed". Ownership-gated - only the
/// uploader can remove their own media. See RemoveMediaRequest's doc
/// comment for why CascadeDeletesEngagement is accepted but unused.
///
/// ADR-031: used to be a genuine session.Delete(mediaAsset) - event streams
/// don't support that, so this now appends MediaAssetRemovedV1 (a flag,
/// see that event's own doc comment) instead. Removing an already-removed
/// asset 404s, same observable behavior as the old hard-delete (a second
/// LoadAsync would have returned null).
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

        var stream = await session.Events.FetchForWriting<MediaAsset>(mediaAssetId, cancellationToken);
        var mediaAsset = stream.Aggregate;
        if (mediaAsset is null || mediaAsset.IsRemoved)
            return TypedResults.NotFound();

        if (mediaAsset.OwnerId != callerOwnerId)
            return TypedResults.Forbid();

        var @event = mediaAsset.Remove();
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok();
    }
}
