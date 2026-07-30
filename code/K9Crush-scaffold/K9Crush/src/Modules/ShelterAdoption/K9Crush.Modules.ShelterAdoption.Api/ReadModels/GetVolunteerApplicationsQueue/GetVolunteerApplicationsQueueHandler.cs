using Marten;
using Microsoft.AspNetCore.Authorization;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.ReadModels.GetVolunteerApplicationsQueue;

/// <summary>
/// State-view slice: EVENT(s) -> READMODEL -> SCREEN. Direct document
/// query over VolunteerApplication, covers Spec/K9CRUSH.emlang.v3.yaml's
/// VolunteeringAndHomeChecks chapter's "Volunteer Applications Queue" view
/// - every volunteer application platform-wide, unfiltered by status, same
/// reasoning as GetFosterApplicationsQueueHandler.
/// </summary>
public static class GetVolunteerApplicationsQueueHandler
{
    [WolverineGet("/api/v1/shelter-adoption/volunteer-applications")]
    [Authorize(Policy = "Admin")]
    public static async Task<VolunteerApplicationsQueueResponse> Handle(
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var applications = await session.Query<VolunteerApplication>().ToListAsync(cancellationToken);

        var items = applications
            .Select(x => new VolunteerApplicationSummary(x.Id, x.ApplicantOwnerId, x.AreaOfInterest.ToString(), x.Status.ToString()))
            .ToList();

        return new VolunteerApplicationsQueueResponse(items);
    }
}
