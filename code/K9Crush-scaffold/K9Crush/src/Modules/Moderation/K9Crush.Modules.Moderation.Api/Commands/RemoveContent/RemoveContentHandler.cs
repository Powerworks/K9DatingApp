using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Moderation.Contracts;
using K9Crush.Modules.Moderation.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Moderation.Api.Commands.RemoveContent;

/// <summary>
/// State-change slice: the emlang yaml's ModeratingFlaggedContentUserReports
/// chapter's "Remove Content" -> "Content Removed". Moderation doesn't
/// own the underlying content (a MediaAsset today) so it can't delete it
/// directly - cascades ContentRemovalRequestedV1 instead, consumed by
/// whichever module owns that content type (see Media's
/// RemoveMediaOnContentRemovalRequestedHandler). No status guard - same
/// "yaml shows no given precondition" reasoning as DismissFlagHandler.
/// </summary>
public static class RemoveContentHandler
{
    [WolverinePost("/api/v1/moderation/flags/{flagId:guid}/remove-content")]
    [Authorize(Policy = "Admin")]
    public static async Task<(Results<Ok<RemoveContentResponse>, NotFound>, ContentRemovalRequestedV1?)> Handle(
        Guid flagId, IDocumentSession session, CancellationToken cancellationToken)
    {
        var flag = await session.LoadAsync<FlaggedContent>(flagId, cancellationToken);
        if (flag is null)
            return (TypedResults.NotFound(), null);

        flag.MarkContentRemoved();
        session.Store(flag);
        await session.SaveChangesAsync(cancellationToken);

        var integrationEvent = new ContentRemovalRequestedV1(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            FlagId: flag.Id,
            ContentType: flag.ContentType.ToString(),
            ContentId: flag.ContentId);

        return (TypedResults.Ok(new RemoveContentResponse(flag.Id, flag.Status.ToString())), integrationEvent);
    }
}
