using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine;
using K9Crush.Modules.ShelterAdoption.Api.Automations.MarkApplicationStale;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.RequestAdditionalDetails;

/// <summary>
/// State-change slice: the emlang yaml's "Request Additional Details" ->
/// "Additional Details Requested" - only valid from UnderReview. Gated by
/// Shelter policy + ownership check, same pattern as
/// ReviewApplicationHandler.
///
/// Also the trigger point for the ShelterReviewsApplication chapter's
/// time-based pair (ADR-026): ShelterAdoption is a document-store module
/// with no domain event stream to subscribe an automation to, so this
/// handler schedules the "please check now" message itself (via
/// IMessageBus.ScheduleAsync) rather than the automation reacting to a
/// stored event. The actual stale-or-not decision still lives entirely in
/// MarkApplicationStaleHandler, which re-checks the application's current
/// status before acting - this line only starts the 15-day clock, it
/// doesn't decide anything.
/// </summary>
public static class RequestAdditionalDetailsHandler
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromDays(15);

    [WolverinePost("/api/v1/shelter-adoption/applications/{applicationId:guid}/request-additional-details")]
    [Authorize(Policy = "Shelter")]
    public static async Task<Results<Ok<RequestAdditionalDetailsResponse>, NotFound, ForbidHttpResult, Conflict<string>>> Handle(
        Guid applicationId,
        RequestAdditionalDetailsRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        IMessageBus bus,
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

        if (application.Status != ApplicationStatus.UnderReview)
            return TypedResults.Conflict($"Cannot request additional details on an application in status {application.Status}.");

        var @event = application.RequestAdditionalDetails(request.Reason);
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);

        await bus.ScheduleAsync(new CheckApplicationStale(application.Id), StaleAfter);

        return TypedResults.Ok(new RequestAdditionalDetailsResponse(application.Id, application.Status.ToString()));
    }
}
