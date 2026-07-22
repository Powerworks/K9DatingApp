using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Contracts;
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
/// Now cascades ApplicationApprovedV1 (ADR-027) - the emlang yaml's "Send
/// Approval Notification" -> "Approval Notification Sent", consumed by
/// Notifications' NotifyOnApplicationApprovedHandler. Previously deferred
/// pending a Notifications module to exist at all.
///
/// v3 ENRICHMENT (Spec/K9CRUSH.emlang.v3.yaml's ShelterManagingListings
/// chapter comment): also cascades the DogListing's Status to Adopted -
/// a same-module state change, not a cross-module integration event, so
/// it's stored in the same session/SaveChangesAsync as the Application
/// itself rather than routed through the message bus.
/// </summary>
public static class ApproveApplicationHandler
{
    [WolverinePost("/api/v1/shelter-adoption/applications/{applicationId:guid}/approve")]
    [Authorize(Policy = "Shelter")]
    public static async Task<(Results<Ok<ApproveApplicationResponse>, NotFound, ForbidHttpResult, Conflict<string>>, ApplicationApprovedV1?)> Handle(
        Guid applicationId,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var application = await session.LoadAsync<Application>(applicationId, cancellationToken);
        if (application is null)
            return (TypedResults.NotFound(), null);

        var shelterAccount = await session.LoadAsync<ShelterAccount>(application.ShelterAccountId, cancellationToken);
        if (shelterAccount is null || shelterAccount.RequestedByOwnerId != callerOwnerId)
            return (TypedResults.Forbid(), null);

        if (application.Status != ApplicationStatus.UnderReview)
            return (TypedResults.Conflict($"Cannot approve an application in status {application.Status}."), null);

        application.Approve();
        session.Store(application);

        var dogListing = await session.LoadAsync<DogListing>(application.DogListingId, cancellationToken);
        if (dogListing is not null)
        {
            dogListing.UpdateStatus(DogListingStatus.Adopted);
            session.Store(dogListing);
        }

        await session.SaveChangesAsync(cancellationToken);

        var integrationEvent = new ApplicationApprovedV1(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            ApplicationId: application.Id,
            ApplicantOwnerId: application.ApplicantOwnerId,
            DogListingId: application.DogListingId,
            DogName: dogListing?.Name ?? string.Empty);

        return (TypedResults.Ok(new ApproveApplicationResponse(application.Id, application.Status.ToString())), integrationEvent);
    }
}
