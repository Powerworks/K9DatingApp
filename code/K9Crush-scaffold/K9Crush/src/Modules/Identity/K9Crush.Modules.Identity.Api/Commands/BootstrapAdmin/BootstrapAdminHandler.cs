using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.BuildingBlocks.Domain;
using K9Crush.Modules.Identity.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Identity.Api.Commands.BootstrapAdmin;

/// <summary>
/// State-change slice: the first-admin bootstrap gap flagged in
/// OwnerAccount.PromoteToAdmin()'s doc comment and previously in
/// GETTING_STARTED.md (a manual Postgres seed). Self-promotes the caller
/// to Admin - always "me" from the JWT, never an arbitrary target owner,
/// so a malicious first-mover can't promote someone else's account during
/// the bootstrap window.
///
/// Only works while zero Admins exist anywhere in the system - once the
/// first Admin is created, this permanently 409s for everyone, including
/// the admin who just bootstrapped. There's no way to reverse this via
/// the API (matches PromoteToShelter()'s "no general-purpose AssignRole
/// endpoint" stance) - a genuine second-admin or admin-recovery need is a
/// deliberately separate, later problem, not solved by this endpoint.
///
/// Same "VerifiedOwner" policy as every other authenticated slice - an
/// unconfirmed Supabase email shouldn't be able to bootstrap into Admin.
/// </summary>
public static class BootstrapAdminHandler
{
    [WolverinePost("/api/v1/identity/me/bootstrap-admin")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<BootstrapAdminResponse>, NotFound, Conflict<string>>> Handle(
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var anyAdminExists = await session.Query<OwnerAccount>()
            .AnyAsync(x => x.Role == OwnerRole.Admin, cancellationToken);
        if (anyAdminExists)
            return TypedResults.Conflict("An admin already exists - bootstrap is only available before the first admin is created.");

        var owner = await session.LoadAsync<OwnerAccount>(callerOwnerId, cancellationToken);
        if (owner is null)
            return TypedResults.NotFound();

        owner.PromoteToAdmin();
        session.Store(owner);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new BootstrapAdminResponse(owner.Id, owner.Role.ToString()));
    }
}
