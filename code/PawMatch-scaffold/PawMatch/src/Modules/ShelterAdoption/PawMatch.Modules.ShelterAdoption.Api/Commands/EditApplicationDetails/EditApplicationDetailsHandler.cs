using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using PawMatch.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace PawMatch.Modules.ShelterAdoption.Api.Commands.EditApplicationDetails;

/// <summary>
/// State-change slice: the emlang yaml's "Edit Application Details" ->
/// "Application Details Edited" - only valid from Draft (once submitted,
/// an application's content is fixed; RequestAdditionalDetails/
/// SubmitAdditionalDetails is the separate, already-built mechanism for
/// amending a submitted application under shelter review).
///
/// Ownership-gated to the applicant, same pattern as
/// ResumeDraftApplicationHandler.
/// </summary>
public static class EditApplicationDetailsHandler
{
    [WolverinePost("/api/v1/shelter-adoption/applications/{applicationId:guid}/edit-details")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<EditApplicationDetailsResponse>, NotFound, ForbidHttpResult, Conflict<string>>> Handle(
        Guid applicationId,
        EditApplicationDetailsRequest request,
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

        if (application.Status != ApplicationStatus.Draft)
            return TypedResults.Conflict($"Cannot edit an application in status {application.Status}.");

        application.EditDetails(request.Details);
        session.Store(application);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new EditApplicationDetailsResponse(application.Id, application.LastEditedAt!.Value));
    }
}
