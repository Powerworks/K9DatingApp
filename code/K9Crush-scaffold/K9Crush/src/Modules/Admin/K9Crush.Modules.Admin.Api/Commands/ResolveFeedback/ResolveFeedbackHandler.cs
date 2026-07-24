using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Admin.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Admin.Api.Commands.ResolveFeedback;

/// <summary>
/// State-change slice: the emlang yaml's HandlingGeneralFeedbackSupport
/// chapter's "Resolve Feedback" -> "Feedback Resolved" - only valid after
/// a response (the yaml's own test, FeedbackResolvedAfterAReply, has
/// "given: Feedback Responded"). State-guard lives in the handler, same
/// convention as every other entity in this codebase.
/// </summary>
public static class ResolveFeedbackHandler
{
    [WolverinePost("/api/v1/admin/feedback/{feedbackId:guid}/resolve")]
    [Authorize(Policy = "Admin")]
    public static async Task<Results<Ok<ResolveFeedbackResponse>, NotFound, Conflict<string>>> Handle(
        Guid feedbackId, IDocumentSession session, CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchForWriting<FeedbackInboxItem>(feedbackId, cancellationToken);
        var item = stream.Aggregate;
        if (item is null)
            return TypedResults.NotFound();

        if (item.Status != FeedbackStatus.Responded)
            return TypedResults.Conflict($"Cannot resolve feedback in status {item.Status} - it must be responded to first.");

        var @event = item.Resolve();
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new ResolveFeedbackResponse(item.Id, item.Status.ToString()));
    }
}
