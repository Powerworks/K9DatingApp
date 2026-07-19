using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using PawMatch.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace PawMatch.Modules.ShelterAdoption.Api.Commands.SubmitAdditionalDetails;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record SubmitAdditionalDetailsResponse(Guid ApplicationId, string Status);

/// <summary>
/// State-change slice: the emlang yaml's "Submit Additional Details" ->
/// "Additional Details Submitted" - only valid from ReturnedForAlteration.
///
/// The yaml attributes this step to the "Shelter Staff" swimlane, but
/// that's clearly a board-labeling artifact - this is the *applicant*
/// responding to RequestAdditionalDetailsHandler's request, not a
/// reviewer action (same kind of departure from the yaml's literal actor
/// label already made once this build-out, for Identity's sign-up
/// fragment). Ownership-gated to the applicant (VerifiedOwner + caller ==
/// ApplicantOwnerId), not Shelter role.
///
/// No request body - the yaml doesn't specify what "additional details"
/// content looks like beyond the reason text captured on the request
/// side (RequestAdditionalDetailsHandler); this is purely the state
/// transition back to review. Add a body if a real form field shows up.
/// </summary>
public static class SubmitAdditionalDetailsHandler
{
    [WolverinePost("/api/v1/shelter-adoption/applications/{applicationId:guid}/submit-additional-details")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<SubmitAdditionalDetailsResponse>, NotFound, ForbidHttpResult, Conflict<string>>> Handle(
        Guid applicationId,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var application = await session.LoadAsync<Application>(applicationId, cancellationToken);
        if (application is null)
            return TypedResults.NotFound();

        if (application.ApplicantOwnerId != callerOwnerId)
            return TypedResults.Forbid();

        if (application.Status != ApplicationStatus.ReturnedForAlteration)
            return TypedResults.Conflict($"Cannot submit additional details on an application in status {application.Status}.");

        application.SubmitAdditionalDetails();
        session.Store(application);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new SubmitAdditionalDetailsResponse(application.Id, application.Status.ToString()));
    }
}
