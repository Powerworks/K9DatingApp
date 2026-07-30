using Marten;
using Microsoft.AspNetCore.Authorization;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.ReadModels.GetFosterApplicationsQueue;

/// <summary>
/// State-view slice: EVENT(s) -> READMODEL -> SCREEN. Direct document
/// query over FosterApplication, covers Spec/K9CRUSH.emlang.v3.yaml's
/// FosteringADog chapter's "Foster Applications Queue" view - every
/// foster application platform-wide, unfiltered by status, same reasoning
/// as GetSurrenderReviewQueueHandler.
/// </summary>
public static class GetFosterApplicationsQueueHandler
{
    [WolverineGet("/api/v1/shelter-adoption/foster-applications")]
    [Authorize(Policy = "Admin")]
    public static async Task<FosterApplicationsQueueResponse> Handle(
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var applications = await session.Query<FosterApplication>().ToListAsync(cancellationToken);

        var items = applications
            .Select(x => new FosterApplicationSummary(x.Id, x.ApplicantOwnerId, x.Status.ToString()))
            .ToList();

        return new FosterApplicationsQueueResponse(items);
    }
}
