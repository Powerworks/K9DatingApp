using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Identity.Contracts;
using K9Crush.Modules.Identity.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Identity.Api.Commands.SubmitFeedback;

/// <summary>
/// State-change slice: the emlang yaml's AccountProfileSettings chapter's
/// "Submit Feedback" -> "Feedback Submitted" (also HandlingGeneralFeedbackSupport's
/// entry point). Cascades FeedbackSubmittedV1 cross-module, consumed by
/// the Admin module's FeedbackSubmittedProjectorHandler to populate its
/// own Feedback Inbox read model - see that handler's doc comment. Not
/// gated on the deletion saga at all - a member can submit feedback any
/// time, independent of account settings/deletion state.
/// </summary>
public static class SubmitFeedbackHandler
{
    [WolverinePost("/api/v1/identity/me/feedback")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<(Ok<SubmitFeedbackResponse>, FeedbackSubmittedV1)> Handle(
        SubmitFeedbackRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var ownerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var (feedback, @event) = Feedback.Submit(ownerId, request.Message);
        session.Events.StartStream<Feedback>(feedback.Id, @event);
        await session.SaveChangesAsync(cancellationToken);

        var integrationEvent = new FeedbackSubmittedV1(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            FeedbackId: feedback.Id,
            OwnerId: feedback.OwnerId,
            Message: feedback.Message,
            SubmittedAt: feedback.SubmittedAt);

        return (TypedResults.Ok(new SubmitFeedbackResponse(feedback.Id)), integrationEvent);
    }
}
