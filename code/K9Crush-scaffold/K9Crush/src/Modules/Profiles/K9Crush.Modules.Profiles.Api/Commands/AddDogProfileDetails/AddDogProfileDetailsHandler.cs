using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Profiles.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Profiles.Api.Commands.AddDogProfileDetails;

/// <summary>
/// State-change slice: the emlang yaml's AddDogProfile chapter's "Add Dog
/// Profile Details" -> "Dog Profile Details Added" - only valid from
/// Draft. Ownership-gated, same pattern as EditApplicationDetailsHandler.
/// </summary>
public static class AddDogProfileDetailsHandler
{
    [WolverinePost("/api/v1/profiles/dogs/{dogProfileId:guid}/details")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<AddDogProfileDetailsResponse>, NotFound, ForbidHttpResult, Conflict<string>>> Handle(
        Guid dogProfileId,
        AddDogProfileDetailsRequest request,
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
            return TypedResults.Conflict($"Cannot add details to a dog profile in status {dogProfile.Status}.");

        var location = GeoCoordinate.Create(request.Latitude, request.Longitude);
        dogProfile.AddDetails(request.Name, request.Breed, request.AgeInMonths, request.Bio, location);
        session.Store(dogProfile);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new AddDogProfileDetailsResponse(dogProfile.Id));
    }
}
