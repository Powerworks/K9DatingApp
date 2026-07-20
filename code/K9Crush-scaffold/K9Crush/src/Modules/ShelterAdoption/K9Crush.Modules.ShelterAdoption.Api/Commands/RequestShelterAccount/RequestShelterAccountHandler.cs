using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.RequestShelterAccount;

/// <summary>
/// State-change slice: SCREEN/CALLER -> COMMAND -> nothing published yet.
/// Unlike CreateDogProfileHandler, this doesn't cascade an integration
/// event - nothing in this module or any other reacts to a shelter
/// account request as an event (verification/approval are later commands
/// run directly against this document by a reviewer, not automations
/// triggered by ShelterAccountRequested). Add one if and when a real
/// consumer needs it (e.g. a future Notifications module wanting to
/// alert reviewers) rather than speculatively now.
/// </summary>
public static class RequestShelterAccountHandler
{
    [WolverinePost("/api/v1/shelter-adoption/shelter-accounts")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<RequestShelterAccountResponse> Handle(
        RequestShelterAccountRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var ownerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var shelterAccount = ShelterAccount.Create(
            ownerId,
            request.BusinessDetails,
            request.UtilityBillDocumentId);

        session.Store(shelterAccount);
        await session.SaveChangesAsync(cancellationToken);

        return new RequestShelterAccountResponse(shelterAccount.Id);
    }
}
