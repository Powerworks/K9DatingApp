using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.ReviewVolunteerApplication;

/// <summary>
/// State-change slice: Spec/K9CRUSH.emlang.v3.yaml's
/// VolunteeringAndHomeChecks chapter, "Review Volunteer Application" ->
/// "Volunteer Application Reviewed" - only valid from Submitted. Admin
/// policy, no ownership check needed - same reasoning as
/// ReviewFosterApplicationHandler (volunteer program administration is
/// Admin-level per the yaml's own swimlane, not scoped to any one shelter).
/// </summary>
public static class ReviewVolunteerApplicationHandler
{
    [WolverinePost("/api/v1/shelter-adoption/volunteer-applications/{volunteerApplicationId:guid}/review")]
    [Authorize(Policy = "Admin")]
    public static async Task<Results<Ok<ReviewVolunteerApplicationResponse>, NotFound, Conflict<string>>> Handle(
        Guid volunteerApplicationId,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchForWriting<VolunteerApplication>(volunteerApplicationId, cancellationToken);
        var volunteerApplication = stream.Aggregate;
        if (volunteerApplication is null)
            return TypedResults.NotFound();

        if (volunteerApplication.Status != VolunteerApplicationStatus.Submitted)
            return TypedResults.Conflict($"Cannot review a volunteer application in status {volunteerApplication.Status}.");

        var @event = volunteerApplication.Review();
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new ReviewVolunteerApplicationResponse(volunteerApplication.Id, volunteerApplication.Status.ToString()));
    }
}
