using Marten;
using Microsoft.AspNetCore.Authorization;
using K9Crush.Modules.Moderation.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Moderation.Api.ReadModels.GetModerationQueue;

/// <summary>
/// State-view slice: the emlang yaml's ModeratingFlaggedContentUserReports
/// chapter's "Moderation Queue" view. Filtered to Open - same "a queue is
/// what still needs attention, not a full history" reasoning as
/// GetPendingApplicationsQueueHandler; resolved flags stay visible via
/// GetFlaggedContentDetailHandler to whoever looks up that specific flag.
/// </summary>
public static class GetModerationQueueHandler
{
    [WolverineGet("/api/v1/moderation/flags")]
    [Authorize(Policy = "Admin")]
    public static async Task<ModerationQueueResponse> Handle(IQuerySession session, CancellationToken cancellationToken)
    {
        var flags = await session.Query<FlaggedContent>()
            .Where(x => x.Status == FlaggedContentStatus.Open)
            .ToListAsync(cancellationToken);

        var items = flags
            .Select(x => new FlaggedContentEntry(
                x.Id, x.ContentType.ToString(), x.ContentId, x.ContentOwnerId, x.ReporterOwnerId, x.FlaggedAt, x.Status.ToString()))
            .ToList();

        return new ModerationQueueResponse(items);
    }
}
