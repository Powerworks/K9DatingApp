using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Identity.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Identity.Api.Commands.RecoverAccount;

/// <summary>
/// State-change slice: the emlang yaml's AccountProfileSettings chapter's
/// "Log In During Grace Period" -> "Account Recovered". Per ADR-005,
/// Identity never implements login itself (Supabase owns it, and
/// Supabase's Database Webhooks only fire on auth.users INSERT/UPDATE,
/// never on an ordinary sign-in) - so this is exposed as an explicit
/// command the client calls right after a successful Supabase login,
/// rather than an automation reacting to a webhook the way
/// ProvisionOwnerOnSupabaseSignup/VerifyOwnerOnSupabaseConfirmation do. A
/// disclosed judgment call, not a deviation discovered by accident.
/// </summary>
public static class RecoverAccountHandler
{
    [WolverinePost("/api/v1/identity/me/recover-account")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<RecoverAccountResponse>, NotFound, Conflict<string>>> Handle(
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var ownerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var stream = await session.Events.FetchForWriting<OwnerAccount>(ownerId, cancellationToken);
        var ownerAccount = stream.Aggregate;
        if (ownerAccount is null)
            return TypedResults.NotFound();

        if (ownerAccount.GracePeriodEndsAt is null || ownerAccount.GracePeriodEndsAt <= DateTimeOffset.UtcNow)
            return TypedResults.Conflict("This account is not within a recoverable grace period.");

        var @event = ownerAccount.RecoverAccount();
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new RecoverAccountResponse(ownerAccount.Id));
    }
}
