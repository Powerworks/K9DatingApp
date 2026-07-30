using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Identity.Contracts;
using K9Crush.Modules.Identity.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Identity.Api.Commands.RequestAccountDeletion;

/// <summary>
/// State-change slice: the emlang yaml's AccountProfileSettings chapter's
/// "Request Account Deletion" -> "Account Deletion Requested". Cascades
/// AccountDeletionRequestedV1 cross-module, consumed by ShelterAdoption's
/// Automations/WithdrawApplicationsOnAccountDeletionRequested to silently
/// withdraw the caller's own open applications - the yaml's "Withdraw
/// Applications Before Deletion" step.
///
/// Deliberately does NOT build the yaml's "Open Items Flagged" step (with
/// openApplicationsCount/openPlaydatesCount props) - showing a live count
/// back to the caller here would need either a forbidden direct
/// cross-module query (see docs/03-solution-architecture.md's module
/// isolation rule) or a second round-trip integration event nothing else
/// yet needs, and openPlaydatesCount has no real source anyway (no
/// Scheduling module exists). Also does not build the shelter-side "Flag
/// Active Listings" branch - the yaml doesn't specify what happens to a
/// shelter's listings after they're flagged, so there's no clear slice to
/// build yet. Both are disclosed gaps, not oversights.
///
/// This only records the request - ConfirmAccountDeletionHandler is the
/// separate, explicit step that actually starts the recoverable grace
/// period (matching the yaml's own "Request Account Deletion" and
/// "Confirm Account Deletion" being two distinct commands/actors).
/// </summary>
public static class RequestAccountDeletionHandler
{
    [WolverinePost("/api/v1/identity/me/request-deletion")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<(Results<Ok<RequestAccountDeletionResponse>, NotFound, Conflict<string>>, AccountDeletionRequestedV1?)> Handle(
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var ownerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var stream = await session.Events.FetchForWriting<OwnerAccount>(ownerId, cancellationToken);
        var ownerAccount = stream.Aggregate;
        if (ownerAccount is null)
            return (TypedResults.NotFound(), null);

        if (ownerAccount.IsPermanentlyDeleted)
            return (TypedResults.Conflict("This account has been permanently deleted."), null);

        if (ownerAccount.DeletionRequestedAt is not null)
            return (TypedResults.Conflict("Account deletion has already been requested."), null);

        var domainEvent = ownerAccount.RequestDeletion();
        stream.AppendOne(domainEvent);
        await session.SaveChangesAsync(cancellationToken);

        var integrationEvent = new AccountDeletionRequestedV1(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            OwnerId: ownerAccount.Id);

        return (TypedResults.Ok(new RequestAccountDeletionResponse(ownerAccount.Id, ownerAccount.DeletionRequestedAt!.Value)), integrationEvent);
    }
}
