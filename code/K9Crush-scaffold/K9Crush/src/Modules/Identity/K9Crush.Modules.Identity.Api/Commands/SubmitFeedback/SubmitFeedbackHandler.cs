using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Identity.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Identity.Api.Commands.SubmitFeedback;

/// <summary>
/// State-change slice: the emlang yaml's AccountProfileSettings chapter's
/// "Submit Feedback" -> "Feedback Submitted" (also HandlingGeneralFeedbackSupport's
/// entry point). No Admin module exists yet to review these (see
/// module_boundaries memory / docs/03-solution-architecture.md's proposed
/// module map) - this only stores the submission (Feedback.cs), same "no
/// destination module yet" deferral as this codebase's other similar gaps.
/// Not gated on the deletion saga at all - a member can submit feedback
/// any time, independent of account settings/deletion state.
/// </summary>
public static class SubmitFeedbackHandler
{
    [WolverinePost("/api/v1/identity/me/feedback")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Ok<SubmitFeedbackResponse>> Handle(
        SubmitFeedbackRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var ownerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var feedback = Feedback.Submit(ownerId, request.Message);
        session.Store(feedback);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new SubmitFeedbackResponse(feedback.Id));
    }
}
