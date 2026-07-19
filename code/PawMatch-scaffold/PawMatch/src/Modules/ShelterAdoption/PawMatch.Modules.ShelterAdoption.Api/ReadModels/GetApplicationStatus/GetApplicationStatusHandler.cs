using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using PawMatch.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace PawMatch.Modules.ShelterAdoption.Api.ReadModels.GetApplicationStatus;

/// <summary>
/// State-view slice: EVENT(s) -> READMODEL -> SCREEN. Direct document
/// query, covers the emlang yaml's "Open Application Status Page"
/// command+event pair and the "Application Status" view as one slice,
/// same consolidation applied throughout this build-out.
///
/// Ownership-gated to the applicant - same reasoning as
/// WithdrawApplicationHandler.
/// </summary>
public static class GetApplicationStatusHandler
{
    [WolverineGet("/api/v1/shelter-adoption/applications/{applicationId:guid}")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<ApplicationStatusResponse>, NotFound, ForbidHttpResult>> Handle(
        Guid applicationId,
        ClaimsPrincipal user,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var application = await session.LoadAsync<Application>(applicationId, cancellationToken);
        if (application is null)
            return TypedResults.NotFound();

        if (application.ApplicantOwnerId != callerOwnerId)
            return TypedResults.Forbid();

        return TypedResults.Ok(new ApplicationStatusResponse(
            application.Id,
            application.DogListingId,
            application.Status.ToString(),
            application.AdditionalDetailsRequestReason,
            application.RejectionReason));
    }
}
