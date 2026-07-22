using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Profiles.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Profiles.Api.Commands.AddDogProfilePhoto;

/// <summary>
/// State-change slice: the emlang yaml's AddDogProfile chapter's "Add Dog
/// Profile Photo" -> "Dog Profile Photo Added" - only valid from Draft.
/// Ownership-gated, same pattern as AddDogProfileDetailsHandler.
/// </summary>
public static class AddDogProfilePhotoHandler
{
    [WolverinePost("/api/v1/profiles/dogs/{dogProfileId:guid}/photos")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<AddDogProfilePhotoResponse>, NotFound, ForbidHttpResult, Conflict<string>>> Handle(
        Guid dogProfileId,
        AddDogProfilePhotoRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var dogProfile = await session.LoadAsync<DogProfile>(dogProfileId, cancellationToken);
        if (dogProfile is null)
            return TypedResults.NotFound();

        if (dogProfile.OwnerId != callerOwnerId)
            return TypedResults.Forbid();

        if (dogProfile.Status != DogProfileStatus.Draft)
            return TypedResults.Conflict($"Cannot add a photo to a dog profile in status {dogProfile.Status}.");

        dogProfile.AttachPhoto(request.MediaAssetId);
        session.Store(dogProfile);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new AddDogProfilePhotoResponse(dogProfile.Id, dogProfile.PhotoIds));
    }
}
