using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Identity.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Identity.Api.Commands.UpdateProfileDetails;

/// <summary>
/// State-change slice: the emlang yaml's AccountProfileSettings chapter's
/// "Update Profile Details" -> "Profile Details Updated". Rejects the
/// update once permanent deletion has actually happened - a recoverable
/// pending-deletion account can still edit its own settings (it might yet
/// be recovered), same reasoning RecoverAccountHandler uses.
/// </summary>
public static class UpdateProfileDetailsHandler
{
    [WolverinePost("/api/v1/identity/me/profile-details")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<UpdateProfileDetailsResponse>, NotFound, Conflict<string>>> Handle(
        UpdateProfileDetailsRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var ownerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var ownerAccount = await session.LoadAsync<OwnerAccount>(ownerId, cancellationToken);
        if (ownerAccount is null)
            return TypedResults.NotFound();

        if (ownerAccount.IsPermanentlyDeleted)
            return TypedResults.Conflict("This account has been permanently deleted.");

        ownerAccount.UpdateDisplayName(request.DisplayName);
        session.Store(ownerAccount);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new UpdateProfileDetailsResponse(ownerAccount.Id, ownerAccount.DisplayName!));
    }
}
