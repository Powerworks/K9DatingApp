using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Media.Contracts;
using K9Crush.Modules.Media.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Media.Api.Commands.ReportMedia;

/// <summary>
/// State-change slice: the emlang yaml's UploadShareRemovePhotosAndVideos
/// chapter's "Report Media" -> "Content Flagged". Deliberately NOT
/// ownership-gated - unlike Share/Remove, reporting content is something
/// any other member does about someone else's media, not the uploader's
/// own action. Cascades MediaContentFlaggedV1 - see that contract's own
/// doc comment for why nothing consumes it yet.
/// </summary>
public static class ReportMediaHandler
{
    [WolverinePost("/api/v1/media/{mediaAssetId:guid}/report")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<(Results<Ok<ReportMediaResponse>, NotFound>, MediaContentFlaggedV1?)> Handle(
        Guid mediaAssetId,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var reporterOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var mediaAsset = await session.LoadAsync<MediaAsset>(mediaAssetId, cancellationToken);
        if (mediaAsset is null)
            return (TypedResults.NotFound(), null);

        var integrationEvent = new MediaContentFlaggedV1(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            MediaAssetId: mediaAsset.Id,
            ContentOwnerId: mediaAsset.OwnerId,
            ReporterOwnerId: reporterOwnerId);

        return (TypedResults.Ok(new ReportMediaResponse(mediaAsset.Id)), integrationEvent);
    }
}
