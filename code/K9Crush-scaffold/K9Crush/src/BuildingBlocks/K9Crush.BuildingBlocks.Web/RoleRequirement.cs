using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.BuildingBlocks.Web;

/// <summary>
/// ASP.NET Core authorization requirement for ADR-017's role dimension.
/// Lives here (not Api.Host) so every module's csproj - which already
/// references BuildingBlocks.Web for IModule - can use it without a new
/// dependency, and so Api.Host doesn't need a direct reference to any
/// module's Domain project just to wire up authorization policies.
/// </summary>
public sealed class RoleRequirement(OwnerRole requiredRole) : IAuthorizationRequirement
{
    public OwnerRole RequiredRole { get; } = requiredRole;
}

/// <summary>
/// Resolves the caller's role via IOwnerRoleLookup (Identity's
/// implementation, registered in DI by IdentityModule.RegisterServices)
/// and succeeds only on an exact match - Admin does not implicitly
/// satisfy a Shelter requirement or vice versa, matching ADR-017's flat
/// role model (no hierarchy specified).
///
/// Registered as Scoped in Program.cs, not Singleton - IOwnerRoleLookup's
/// Marten implementation depends on the scoped IQuerySession, and a
/// singleton handler capturing a scoped dependency would be a captive-
/// dependency bug (the same IQuerySession reused across every request
/// after the first).
/// </summary>
public sealed class RoleAuthorizationHandler(IOwnerRoleLookup roleLookup) : AuthorizationHandler<RoleRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, RoleRequirement requirement)
    {
        var ownerIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (ownerIdClaim is null || !Guid.TryParse(ownerIdClaim, out var ownerId))
            return;

        var role = await roleLookup.GetRoleAsync(ownerId, CancellationToken.None);
        if (role == requirement.RequiredRole)
            context.Succeed(requirement);
    }
}
