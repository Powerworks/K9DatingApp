using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.WithdrawApplication;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record WithdrawApplicationResponse(Guid ApplicationId, string Status);

/// <summary>
/// State-change slice: the emlang yaml's "Withdraw Application" ->
/// "Application Withdrawn", except when the application is already
/// Approved, which the yaml names as its own outcome ("Withdrawal
/// Blocked: Already Approved") rather than a generic conflict - kept
/// visible as a distinct message, not folded into a generic 409 string,
/// since the yaml treats it as a first-class named event.
///
/// Ownership-gated (VerifiedOwner + applicant check), no Shelter/Admin
/// role involved - withdrawing is entirely the applicant's own call.
/// </summary>
public static class WithdrawApplicationHandler
{
    [WolverinePost("/api/v1/shelter-adoption/applications/{applicationId:guid}/withdraw")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<WithdrawApplicationResponse>, NotFound, ForbidHttpResult, Conflict<string>>> Handle(
        Guid applicationId,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var stream = await session.Events.FetchForWriting<Application>(applicationId, cancellationToken);
        var application = stream.Aggregate;
        if (application is null)
            return TypedResults.NotFound();

        if (application.ApplicantOwnerId != callerOwnerId)
            return TypedResults.Forbid();

        if (application.Status == ApplicationStatus.Approved)
            return TypedResults.Conflict("Withdrawal Blocked: Already Approved.");

        var @event = application.Withdraw();
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new WithdrawApplicationResponse(application.Id, application.Status.ToString()));
    }
}
