using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.RequestAdditionalDetails;

/// <summary>
/// State-change slice: the emlang yaml's "Request Additional Details" ->
/// "Additional Details Requested" - only valid from UnderReview. Gated by
/// Shelter policy + ownership check, same pattern as
/// ReviewApplicationHandler.
/// </summary>
public static class RequestAdditionalDetailsHandler
{
    [WolverinePost("/api/v1/shelter-adoption/applications/{applicationId:guid}/request-additional-details")]
    [Authorize(Policy = "Shelter")]
    public static async Task<Results<Ok<RequestAdditionalDetailsResponse>, NotFound, ForbidHttpResult, Conflict<string>>> Handle(
        Guid applicationId,
        RequestAdditionalDetailsRequest request,
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
            return TypedResults.Conflict($"Cannot request additional details on an application in status {application.Status}.");

        application.RequestAdditionalDetails(request.Reason);
        session.Store(application);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new RequestAdditionalDetailsResponse(application.Id, application.Status.ToString()));
    }
}
