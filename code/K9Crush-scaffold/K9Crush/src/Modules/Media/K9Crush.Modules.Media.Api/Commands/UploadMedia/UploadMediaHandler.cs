using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Media.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Media.Api.Commands.UploadMedia;

/// <summary>
/// State-change slice: the emlang yaml's UploadShareRemovePhotosAndVideos
/// chapter's "Upload Media" -> "Media Uploaded". The chapter's "Upload
/// Failed" -> "Reject Upload" -> "Upload Rejected: Invalid File" branch
/// is handled by UploadMediaRequest's IValidatableObject check (an
/// extension-vs-MediaType mismatch) - Wolverine.Http's DataAnnotations
/// pipeline rejects it with a 400 before this Handle method ever runs,
/// same "validation lives in the request record" convention as every
/// other slice in this codebase (see CLAUDE.md's Code Standards) - no
/// separate RejectUpload command exists.
///
/// ADR-031: starts a brand-new event stream (session.Events.StartStream)
/// rather than session.Store - this is the one command in the module that
/// creates a MediaAsset rather than fetching an existing one.
/// </summary>
public static class UploadMediaHandler
{
    [WolverinePost("/api/v1/media")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Ok<UploadMediaResponse>> Handle(
        UploadMediaRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var ownerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var (mediaAsset, @event) = MediaAsset.Upload(ownerId, request.MediaType, request.StorageUrl);
        session.Events.StartStream<MediaAsset>(mediaAsset.Id, @event);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new UploadMediaResponse(mediaAsset.Id, mediaAsset.MediaType.ToString(), mediaAsset.UploadedAt));
    }
}
