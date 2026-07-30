using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine;
using K9Crush.Modules.Identity.Api.Automations.PermanentlyDeleteAccountAfterGracePeriod;
using K9Crush.Modules.Identity.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Identity.Api.Commands.ConfirmAccountDeletion;

/// <summary>
/// State-change slice: the emlang yaml's AccountProfileSettings chapter's
/// "Confirm Account Deletion" -> "Account Deleted" (gracePeriodDays: 30,
/// recoverable: true). Only valid once RequestAccountDeletionHandler has
/// already run - matches the yaml's own two-step "Request" then "Confirm"
/// shape rather than collapsing them into one command.
///
/// Schedules the "please check now" message itself (ADR-026 - same
/// pattern as ShelterAdoption's RequestAdditionalDetailsHandler): the
/// actual permanent-delete-or-not decision still lives entirely in
/// Automations/PermanentlyDeleteAccountAfterGracePeriod, which re-checks
/// GracePeriodEndsAt is still set and unexpired before acting - this line
/// only starts the 30-day clock.
/// </summary>
public static class ConfirmAccountDeletionHandler
{
    private const int GracePeriodDays = 30;

    [WolverinePost("/api/v1/identity/me/confirm-deletion")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<ConfirmAccountDeletionResponse>, NotFound, Conflict<string>>> Handle(
        ClaimsPrincipal user,
        IDocumentSession session,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var ownerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var stream = await session.Events.FetchForWriting<OwnerAccount>(ownerId, cancellationToken);
        var ownerAccount = stream.Aggregate;
        if (ownerAccount is null)
            return TypedResults.NotFound();

        if (ownerAccount.DeletionRequestedAt is null)
            return TypedResults.Conflict("Account deletion has not been requested yet.");

        if (ownerAccount.GracePeriodEndsAt is not null)
            return TypedResults.Conflict("Account deletion has already been confirmed.");

        var @event = ownerAccount.ConfirmDeletion(GracePeriodDays);
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);

        await bus.ScheduleAsync(new CheckAccountGracePeriodExpired(ownerAccount.Id), TimeSpan.FromDays(GracePeriodDays));

        return TypedResults.Ok(new ConfirmAccountDeletionResponse(
            ownerAccount.Id, ownerAccount.GracePeriodEndsAt!.Value, GracePeriodDays, Recoverable: true));
    }
}
