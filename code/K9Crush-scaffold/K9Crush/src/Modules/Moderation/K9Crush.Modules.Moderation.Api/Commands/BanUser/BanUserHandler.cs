using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Moderation.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Moderation.Api.Commands.BanUser;

/// <summary>
/// State-change slice: the emlang yaml's ModeratingFlaggedContentUserReports
/// chapter's "Ban User" -> "User Banned" - only valid after a prior
/// warning AND a prior suspension (RepeatOffenderBannedAfterWarningAndSuspension's
/// "given: User Warned, User Suspended"). Checking IsSuspended alone is
/// sufficient - SuspendUserHandler's own guard already requires a prior
/// warning before suspension can happen, so IsSuspended being true
/// implies both preconditions transitively.
/// </summary>
public static class BanUserHandler
{
    [WolverinePost("/api/v1/moderation/flags/{flagId:guid}/ban-user")]
    [Authorize(Policy = "Admin")]
    public static async Task<Results<Ok<BanUserResponse>, NotFound, Conflict<string>>> Handle(
        Guid flagId, IDocumentSession session, CancellationToken cancellationToken)
    {
        var flag = await session.LoadAsync<FlaggedContent>(flagId, cancellationToken);
        if (flag is null)
            return TypedResults.NotFound();

        var record = await session.LoadAsync<UserModerationRecord>(flag.ContentOwnerId, cancellationToken);
        if (record is null || !record.IsSuspended)
            return TypedResults.Conflict("Cannot ban an owner who has not been warned and suspended first.");

        record.Ban();
        session.Store(record);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new BanUserResponse(record.Id, record.IsBanned));
    }
}
