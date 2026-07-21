using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Moderation.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Moderation.Api.Commands.WarnUser;

/// <summary>
/// State-change slice: the emlang yaml's ModeratingFlaggedContentUserReports
/// chapter's "Warn User" -> "User Warned" - the first rung of the escalation
/// ladder, no precondition (AdminWarnsAUserForAFirstViolation has no
/// "given"). Routed via the flag (not directly by ownerId) since that's
/// how an admin reaches this action from the Flagged Content Detail
/// screen - resolves the target owner from FlaggedContent.ContentOwnerId.
/// Creates the UserModerationRecord lazily on first warning.
/// </summary>
public static class WarnUserHandler
{
    [WolverinePost("/api/v1/moderation/flags/{flagId:guid}/warn-user")]
    [Authorize(Policy = "Admin")]
    public static async Task<Results<Ok<WarnUserResponse>, NotFound>> Handle(
        Guid flagId, IDocumentSession session, CancellationToken cancellationToken)
    {
        var flag = await session.LoadAsync<FlaggedContent>(flagId, cancellationToken);
        if (flag is null)
            return TypedResults.NotFound();

        var record = await session.LoadAsync<UserModerationRecord>(flag.ContentOwnerId, cancellationToken)
                     ?? UserModerationRecord.CreateFor(flag.ContentOwnerId);

        record.Warn();
        session.Store(record);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new WarnUserResponse(record.Id, record.WarningCount));
    }
}
