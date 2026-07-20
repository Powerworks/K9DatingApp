using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.ResumeDraftApplication;

/// <summary>
/// State-change slice: consolidates the emlang yaml's "Resume Draft
/// Application" -> "Draft Application Resumed" AND "Check Dog
/// Availability On Resume" -> "Draft Application Closed: Dog No Longer
/// Available" into one handler, since checking availability is
/// literally what resuming does before handing the draft back for
/// editing - same "one decision point, multiple outcomes" pattern as
/// SubmitApplicationHandler.
///
/// "No longer available" = the DogListing document no longer exists
/// (RemoveDogListingHandler hard-deletes it) - reuses existing
/// infrastructure rather than adding an availability/status field to
/// DogListing that nothing else needs yet.
///
/// Only valid from Draft. Ownership-gated to the applicant
/// (VerifiedOwner + caller == ApplicantOwnerId), no Shelter/Admin role -
/// resuming your own draft is entirely your own call.
/// </summary>
public static class ResumeDraftApplicationHandler
{
    [WolverinePost("/api/v1/shelter-adoption/applications/{applicationId:guid}/resume")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<ResumeDraftApplicationResponse>, NotFound, ForbidHttpResult, Conflict<string>>> Handle(
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

        if (application.Status != ApplicationStatus.Draft)
            return TypedResults.Conflict($"Cannot resume a draft application in status {application.Status}.");

        var dogListing = await session.LoadAsync<DogListing>(application.DogListingId, cancellationToken);
        if (dogListing is null)
        {
            application.CloseDraftDogNoLongerAvailable();
            session.Store(application);
            await session.SaveChangesAsync(cancellationToken);

            return TypedResults.Ok(new ResumeDraftApplicationResponse(
                application.Id, application.DogListingId, application.Status.ToString(), application.Details));
        }

        return TypedResults.Ok(new ResumeDraftApplicationResponse(
            application.Id, application.DogListingId, application.Status.ToString(), application.Details));
    }
}
