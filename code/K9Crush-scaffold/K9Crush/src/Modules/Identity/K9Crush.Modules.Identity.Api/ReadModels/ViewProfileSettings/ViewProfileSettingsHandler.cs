using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Identity.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Identity.Api.ReadModels.ViewProfileSettings;

/// <summary>
/// State-view slice: the emlang yaml's AccountProfileSettings chapter's
/// "View Profile Settings" -> "Profile Settings Viewed". Direct document
/// read, same shape as OwnerAccountView's GetHandler - see that file's
/// comment for why [Authorize] is not optional here (always resolves
/// "my own account" from the caller's JWT, no route parameter to guard).
/// Deliberately a separate endpoint from OwnerAccountView rather than
/// adding fields there - that slice already shipped and is used
/// elsewhere; this one is scoped to exactly this chapter's needs.
/// </summary>
public static class ViewProfileSettingsHandler
{
    [WolverineGet("/api/v1/identity/me/settings")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<ProfileSettingsResponse>, NotFound>> Handle(
        ClaimsPrincipal user,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var ownerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var ownerAccount = await session.LoadAsync<OwnerAccount>(ownerId, cancellationToken);
        if (ownerAccount is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(new ProfileSettingsResponse(
            ownerAccount.Id,
            ownerAccount.Email,
            ownerAccount.DisplayName,
            ownerAccount.Role.ToString(),
            ownerAccount.CreatedAt,
            ownerAccount.DeletionRequestedAt,
            ownerAccount.GracePeriodEndsAt,
            ownerAccount.IsPermanentlyDeleted));
    }
}
