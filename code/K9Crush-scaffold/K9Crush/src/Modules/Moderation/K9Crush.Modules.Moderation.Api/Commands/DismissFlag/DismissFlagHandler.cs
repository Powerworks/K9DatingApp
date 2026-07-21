using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Moderation.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Moderation.Api.Commands.DismissFlag;

/// <summary>
/// State-change slice: the emlang yaml's ModeratingFlaggedContentUserReports
/// chapter's "Dismiss Flag" -> "Flag Dismissed". The yaml's own test
/// (AdminDismissesAFlagWithNoActionNeeded) has no "given" precondition,
/// so this is valid from any status - same "no guard, the yaml doesn't
/// show one" precedent as Admin's RespondToFeedbackHandler.
/// </summary>
public static class DismissFlagHandler
{
    [WolverinePost("/api/v1/moderation/flags/{flagId:guid}/dismiss")]
    [Authorize(Policy = "Admin")]
    public static async Task<Results<Ok<DismissFlagResponse>, NotFound>> Handle(
        Guid flagId, IDocumentSession session, CancellationToken cancellationToken)
    {
        var flag = await session.LoadAsync<FlaggedContent>(flagId, cancellationToken);
        if (flag is null)
            return TypedResults.NotFound();

        flag.Dismiss();
        session.Store(flag);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new DismissFlagResponse(flag.Id, flag.Status.ToString()));
    }
}
