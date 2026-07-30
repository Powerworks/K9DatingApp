using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Admin.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Admin.Api.Commands.RespondToFeedback;

/// <summary>
/// State-change slice: the emlang yaml's HandlingGeneralFeedbackSupport
/// chapter's "Respond To Feedback" -> "Feedback Responded". The yaml's
/// own test (AdminRepliesToAFeedbackSubmission) has no "given" precondition,
/// so this is valid from any status - including responding again, which
/// simply overwrites the prior response (no "already responded" guard).
/// No cascade back to Notifications to email the owner - the yaml doesn't
/// show that step for this chain (unlike, say, ShelterManagingListings'
/// explicit Notify chain), so none is built.
/// </summary>
public static class RespondToFeedbackHandler
{
    [WolverinePost("/api/v1/admin/feedback/{feedbackId:guid}/respond")]
    [Authorize(Policy = "Admin")]
    public static async Task<Results<Ok<RespondToFeedbackResponse>, NotFound>> Handle(
        Guid feedbackId,
        RespondToFeedbackRequest request,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchForWriting<FeedbackInboxItem>(feedbackId, cancellationToken);
        var item = stream.Aggregate;
        if (item is null)
            return TypedResults.NotFound();

        var @event = item.Respond(request.ResponseMessage);
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new RespondToFeedbackResponse(item.Id, item.Status.ToString()));
    }
}
