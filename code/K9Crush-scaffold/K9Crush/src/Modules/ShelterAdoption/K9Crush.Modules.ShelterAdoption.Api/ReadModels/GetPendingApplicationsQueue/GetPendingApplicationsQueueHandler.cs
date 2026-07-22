using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.ReadModels.GetPendingApplicationsQueue;

/// <summary>
/// State-view slice: EVENT(s) -> READMODEL -> SCREEN. Direct document
/// query, covers the emlang yaml's "Pending Applications Queue" view.
/// Filtered to Application.IsOpen (Pending/UnderReview/ReturnedForAlteration)
/// - a shelter reviewer's queue is "what still needs my attention," not a
/// full history; Approved/Rejected/Withdrawn applications stay visible
/// via GetApplicationStatusHandler to whoever owns them.
///
/// Gated by Shelter policy + ownership check, same pattern as
/// GetShelterDogListingsHandler.
/// </summary>
public static class GetPendingApplicationsQueueHandler
{
    [WolverineGet("/api/v1/shelter-adoption/shelter-accounts/{shelterAccountId:guid}/applications")]
    [Authorize(Policy = "Shelter")]
    public static async Task<Results<Ok<PendingApplicationsQueueResponse>, NotFound, ForbidHttpResult>> Handle(
        Guid shelterAccountId,
        ClaimsPrincipal user,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var shelterAccount = await session.LoadAsync<ShelterAccount>(shelterAccountId, cancellationToken);
        if (shelterAccount is null)
            return TypedResults.NotFound();

        if (shelterAccount.RequestedByOwnerId != callerOwnerId)
            return TypedResults.Forbid();

        var applications = await session.Query<Application>()
            .Where(x => x.ShelterAccountId == shelterAccountId)
            .ToListAsync(cancellationToken);

        var items = applications
            .Where(x => x.IsOpen)
            .Select(x => new PendingApplicationSummary(x.Id, x.DogListingId, x.ApplicantOwnerId, x.Status.ToString()))
            .ToList();

        return TypedResults.Ok(new PendingApplicationsQueueResponse(items));
    }
}
