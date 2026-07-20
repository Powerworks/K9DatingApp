using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.ApproveApplication;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record ApproveApplicationResponse(Guid ApplicationId, string Status);

/// <summary>
/// State-change slice: the emlang yaml's "Approve Application" ->
/// "Application Approved" - only valid from UnderReview. Gated by
/// Shelter policy + ownership check, same pattern as
/// ReviewApplicationHandler.
///
/// Deliberately does NOT cascade "Send Approval Notification" - same
/// "no Notifications module yet" call as RejectApplicationHandler.
/// </summary>
public static class ApproveApplicationHandler
{
    [WolverinePost("/api/v1/shelter-adoption/applications/{applicationId:guid}/approve")]
    [Authorize(Policy = "Shelter")]
    public static async Task<Results<Ok<ApproveApplicationResponse>, NotFound, ForbidHttpResult, Conflict<string>>> Handle(
        Guid applicationId,
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
            return TypedResults.Conflict($"Cannot approve an application in status {application.Status}.");

        application.Approve();
        session.Store(application);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new ApproveApplicationResponse(application.Id, application.Status.ToString()));
    }
}
