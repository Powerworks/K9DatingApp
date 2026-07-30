using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.ApproveVolunteer;

/// <summary>
/// State-change slice: Spec/K9CRUSH.emlang.v3.yaml's
/// VolunteeringAndHomeChecks chapter, "Approve Volunteer" -> "Volunteer
/// Approved" - only valid from UnderReview. Admin policy, no ownership
/// check needed - same reasoning as ApproveFosterCaregiverHandler. Being
/// an "approved volunteer" is just having an Approved VolunteerApplication
/// on file - not a new OwnerRole, same restraint as the foster caregiver
/// case.
/// </summary>
public static class ApproveVolunteerHandler
{
    [WolverinePost("/api/v1/shelter-adoption/volunteer-applications/{volunteerApplicationId:guid}/approve")]
    [Authorize(Policy = "Admin")]
    public static async Task<Results<Ok<ApproveVolunteerResponse>, NotFound, Conflict<string>>> Handle(
        Guid volunteerApplicationId,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchForWriting<VolunteerApplication>(volunteerApplicationId, cancellationToken);
        var volunteerApplication = stream.Aggregate;
        if (volunteerApplication is null)
            return TypedResults.NotFound();

        if (volunteerApplication.Status != VolunteerApplicationStatus.UnderReview)
            return TypedResults.Conflict($"Cannot approve a volunteer application in status {volunteerApplication.Status}.");

        var @event = volunteerApplication.Approve();
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new ApproveVolunteerResponse(volunteerApplication.Id, volunteerApplication.Status.ToString()));
    }
}
