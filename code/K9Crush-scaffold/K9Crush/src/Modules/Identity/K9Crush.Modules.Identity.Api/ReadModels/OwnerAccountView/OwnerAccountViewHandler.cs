using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Identity.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Identity.Api.ReadModels.OwnerAccountView;

/// <summary>
/// State-view slice: EVENT(OwnerRegisteredV1) -> READMODEL -> SCREEN. No
/// separate projector needed - OwnerAccount is already a raw Marten
/// document (Identity is document-centric, not event-sourced per
/// Solution Architecture doc Section 2.1), so this is a direct document
/// read, same shape as GetDogProfileHandler.
///
/// Unlike GetDogProfileHandler/GetDiscoveryFeedHandler (both deliberately
/// left without [Authorize] - a documented product decision in
/// GETTING_STARTED.md Section 6, not an oversight), this slice has no
/// route parameter to look up - it always resolves "my own account" from
/// the caller's own JWT. Without [Authorize], an anonymous request would
/// reach Guid.Parse(user.FindFirstValue(...)!) with a null claim and
/// throw unhandled - the exact bug class CreateDogProfileHandler already
/// hit once (see that file's comment) - so [Authorize] isn't optional
/// here the way it was debatable for those two. Uses the same
/// "VerifiedOwner" policy as every other authenticated slice in this
/// codebase for consistency, though whether an unverified owner should
/// still be able to see their own pending registration is a product
/// question worth revisiting once VerifyEmail exists.
/// </summary>
public static class OwnerAccountViewHandler
{
    [WolverineGet("/api/v1/identity/me")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<OwnerAccountResponse>, NotFound>> Handle(
        ClaimsPrincipal user,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var ownerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var ownerAccount = await session.LoadAsync<OwnerAccount>(ownerId, cancellationToken);

        if (ownerAccount is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(new OwnerAccountResponse(
            ownerAccount.Id,
            ownerAccount.Email,
            ownerAccount.IsVerified,
            ownerAccount.CreatedAt));
    }
}
