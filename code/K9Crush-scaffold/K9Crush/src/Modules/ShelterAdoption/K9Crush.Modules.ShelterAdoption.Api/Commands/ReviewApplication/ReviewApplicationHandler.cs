using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.ReviewApplication;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record ReviewApplicationResponse(Guid ApplicationId, string Status);

/// <summary>
/// State-change slice: the emlang yaml's "Review Application" ->
/// "Application Reviewed" - only valid from Pending. Gated by Shelter
/// policy + ownership check (caller must own the ShelterAccount that
/// owns this Application, via Application.ShelterAccountId), same
/// pattern as EditDogListingHandler.
/// </summary>
public static class ReviewApplicationHandler
{
    [WolverinePost("/api/v1/shelter-adoption/applications/{applicationId:guid}/review")]
    [Authorize(Policy = "Shelter")]
    public static async Task<Results<Ok<ReviewApplicationResponse>, NotFound, ForbidHttpResult, Conflict<string>>> Handle(
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

        var shelterAccount = await session.LoadAsync<ShelterAccount>(application.ShelterAccountId, cancellationToken);
        if (shelterAccount is null || shelterAccount.RequestedByOwnerId != callerOwnerId)
            return TypedResults.Forbid();

        if (application.Status != ApplicationStatus.Pending)
            return TypedResults.Conflict($"Cannot review an application in status {application.Status}.");

        var @event = application.Review();
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new ReviewApplicationResponse(application.Id, application.Status.ToString()));
    }
}
