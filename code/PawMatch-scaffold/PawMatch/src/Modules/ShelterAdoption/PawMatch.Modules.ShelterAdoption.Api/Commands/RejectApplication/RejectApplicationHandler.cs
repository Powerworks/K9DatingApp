using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using PawMatch.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace PawMatch.Modules.ShelterAdoption.Api.Commands.RejectApplication;

/// <summary>
/// State-change slice: the emlang yaml's "Reject Application" ->
/// "Application Rejected" - only valid from UnderReview. Gated by
/// Shelter policy + ownership check, same pattern as
/// ReviewApplicationHandler.
///
/// Deliberately does NOT cascade "Send Rejection Reason" -> "Rejection
/// Reason Sent" - that's a notification, and no Notifications module
/// exists yet (same "no infra, no slice" call made throughout this
/// build-out). RejectionReason is still captured on the Application
/// document itself (visible via GetApplicationStatusHandler), just not
/// pushed anywhere yet.
/// </summary>
public static class RejectApplicationHandler
{
    [WolverinePost("/api/v1/shelter-adoption/applications/{applicationId:guid}/reject")]
    [Authorize(Policy = "Shelter")]
    public static async Task<Results<Ok<RejectApplicationResponse>, NotFound, ForbidHttpResult, Conflict<string>>> Handle(
        Guid applicationId,
        RejectApplicationRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var application = await session.LoadAsync<Application>(applicationId, cancellationToken);
        if (application is null)
            return TypedResults.NotFound();

        var shelterAccount = await session.LoadAsync<ShelterAccount>(application.ShelterAccountId, cancellationToken);
        if (shelterAccount is null || shelterAccount.RequestedByOwnerId != callerOwnerId)
            return TypedResults.Forbid();

        if (application.Status != ApplicationStatus.UnderReview)
            return TypedResults.Conflict($"Cannot reject an application in status {application.Status}.");

        application.Reject(request.Reason);
        session.Store(application);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new RejectApplicationResponse(application.Id, application.Status.ToString()));
    }
}
