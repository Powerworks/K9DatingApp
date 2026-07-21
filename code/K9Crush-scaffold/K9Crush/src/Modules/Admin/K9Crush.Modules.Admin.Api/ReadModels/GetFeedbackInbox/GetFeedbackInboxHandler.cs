using Marten;
using Microsoft.AspNetCore.Authorization;
using K9Crush.Modules.Admin.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Admin.Api.ReadModels.GetFeedbackInbox;

/// <summary>
/// State-view slice: the emlang yaml's HandlingGeneralFeedbackSupport
/// chapter's "Feedback Inbox" view. Direct document query, newest first -
/// no filtering by status, an admin sees every submission regardless of
/// where it is in the Open/Responded/Resolved lifecycle (the yaml doesn't
/// specify inbox filtering, and a small inbox doesn't need it yet).
/// </summary>
public static class GetFeedbackInboxHandler
{
    [WolverineGet("/api/v1/admin/feedback")]
    [Authorize(Policy = "Admin")]
    public static async Task<FeedbackInboxResponse> Handle(IQuerySession session, CancellationToken cancellationToken)
    {
        var items = await session.Query<FeedbackInboxItem>()
            .OrderByDescending(x => x.SubmittedAt)
            .ToListAsync(cancellationToken);

        return new FeedbackInboxResponse(items
            .Select(x => new FeedbackInboxEntry(x.Id, x.OwnerId, x.Message, x.SubmittedAt, x.Status.ToString()))
            .ToList());
    }
}
