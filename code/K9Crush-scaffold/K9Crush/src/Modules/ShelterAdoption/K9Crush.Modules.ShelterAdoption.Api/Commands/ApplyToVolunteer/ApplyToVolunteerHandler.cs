using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.ApplyToVolunteer;

/// <summary>
/// State-change slice: Spec/K9CRUSH.emlang.v3.yaml's VolunteeringAndHomeChecks
/// chapter, "Apply To Volunteer" -> "Volunteer Application Submitted" - a
/// member applying to become an approved volunteer. Any verified owner can
/// apply - no Shelter/Admin role needed, same reasoning as ApplyToFosterHandler.
/// </summary>
public static class ApplyToVolunteerHandler
{
    [WolverinePost("/api/v1/shelter-adoption/volunteer-applications")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Ok<ApplyToVolunteerResponse>> Handle(
        ApplyToVolunteerRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var applicantOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var (volunteerApplication, @event) = VolunteerApplication.ApplyNew(applicantOwnerId, request.AreaOfInterest);
        session.Events.StartStream<VolunteerApplication>(volunteerApplication.Id, @event);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new ApplyToVolunteerResponse(volunteerApplication.Id));
    }
}
