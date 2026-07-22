using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Moderation.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Moderation.Api.ReadModels.GetFlaggedContentDetail;

/// <summary>
/// State-view slice: the emlang yaml's ModeratingFlaggedContentUserReports
/// chapter's "Flagged Content Detail" view. Direct document read, same
/// shape as Admin's GetFeedbackDetailHandler.
/// </summary>
public static class GetFlaggedContentDetailHandler
{
    [WolverineGet("/api/v1/moderation/flags/{flagId:guid}")]
    [Authorize(Policy = "Admin")]
    public static async Task<Results<Ok<FlaggedContentDetailResponse>, NotFound>> Handle(
        Guid flagId, IQuerySession session, CancellationToken cancellationToken)
    {
        var flag = await session.LoadAsync<FlaggedContent>(flagId, cancellationToken);
        if (flag is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(new FlaggedContentDetailResponse(
            flag.Id, flag.ContentType.ToString(), flag.ContentId, flag.ContentOwnerId, flag.ReporterOwnerId, flag.FlaggedAt, flag.Status.ToString()));
    }
}
