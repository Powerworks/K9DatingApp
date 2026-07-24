using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Contracts;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.RejectApplication;

/// <summary>
/// State-change slice: the emlang yaml's "Reject Application" ->
/// "Application Rejected" - only valid from UnderReview. Gated by
/// Shelter policy + ownership check, same pattern as
/// ReviewApplicationHandler.
///
/// Now cascades ApplicationRejectedV1 (ADR-027) - the emlang yaml's "Send
/// Rejection Reason" -> "Rejection Reason Sent", consumed by
/// Notifications' NotifyOnApplicationRejectedHandler. Previously deferred
/// pending a Notifications module to exist at all.
/// </summary>
public static class RejectApplicationHandler
{
    [WolverinePost("/api/v1/shelter-adoption/applications/{applicationId:guid}/reject")]
    [Authorize(Policy = "Shelter")]
    public static async Task<(Results<Ok<RejectApplicationResponse>, NotFound, ForbidHttpResult, Conflict<string>>, ApplicationRejectedV1?)> Handle(
        Guid applicationId,
        RejectApplicationRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var stream = await session.Events.FetchForWriting<Application>(applicationId, cancellationToken);
        var application = stream.Aggregate;
        if (application is null)
            return (TypedResults.NotFound(), null);

        var shelterAccount = await session.LoadAsync<ShelterAccount>(application.ShelterAccountId, cancellationToken);
        if (shelterAccount is null || shelterAccount.RequestedByOwnerId != callerOwnerId)
            return (TypedResults.Forbid(), null);

        if (application.Status != ApplicationStatus.UnderReview)
            return (TypedResults.Conflict($"Cannot reject an application in status {application.Status}."), null);

        var domainEvent = application.Reject(request.Reason);
        stream.AppendOne(domainEvent);
        await session.SaveChangesAsync(cancellationToken);

        var dogListing = await session.LoadAsync<DogListing>(application.DogListingId, cancellationToken);

        var integrationEvent = new ApplicationRejectedV1(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            ApplicationId: application.Id,
            ApplicantOwnerId: application.ApplicantOwnerId,
            DogListingId: application.DogListingId,
            DogName: dogListing?.Name ?? string.Empty,
            RejectionReason: request.Reason);

        return (TypedResults.Ok(new RejectApplicationResponse(application.Id, application.Status.ToString())), integrationEvent);
    }
}
