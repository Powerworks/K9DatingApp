using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Moderation.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Moderation.Api.Commands.SuspendUser;

/// <summary>
/// State-change slice: the emlang yaml's ModeratingFlaggedContentUserReports
/// chapter's "Suspend User" -> "User Suspended" - only valid after at
/// least one prior warning (RepeatOffenderSuspendedAfterAPriorWarning's
/// "given: User Warned"). A UserModerationRecord only ever exists after
/// WarnUserHandler has run at least once (it's the only place one gets
/// created, always immediately incremented past zero), so "no record"
/// and "never warned" are the same condition here.
/// </summary>
public static class SuspendUserHandler
{
    [WolverinePost("/api/v1/moderation/flags/{flagId:guid}/suspend-user")]
    [Authorize(Policy = "Admin")]
    public static async Task<Results<Ok<SuspendUserResponse>, NotFound, Conflict<string>>> Handle(
        Guid flagId, IDocumentSession session, CancellationToken cancellationToken)
    {
        var flag = await session.LoadAsync<FlaggedContent>(flagId, cancellationToken);
        if (flag is null)
            return TypedResults.NotFound();

        var record = await session.LoadAsync<UserModerationRecord>(flag.ContentOwnerId, cancellationToken);
        if (record is null || record.WarningCount < 1)
            return TypedResults.Conflict("Cannot suspend an owner who has not been warned yet.");

        record.Suspend();
        session.Store(record);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new SuspendUserResponse(record.Id, record.IsSuspended));
    }
}
