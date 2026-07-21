using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Profiles.Contracts;
using K9Crush.Modules.Profiles.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Profiles.Api.Commands.PublishDogProfile;

/// <summary>
/// State-change slice: the emlang yaml's AddDogProfile chapter's "Publish
/// Dog Profile" -> "Dog Profile Published", except when there's no photo
/// yet, which the yaml names as its own outcome ("Reject Publish (No
/// Photo)" -> "Publish Blocked: Photo Required") rather than a generic
/// conflict - same "keep the yaml's named outcome visible" pattern as
/// WithdrawApplicationHandler's "Withdrawal Blocked: Already Approved".
///
/// This is also where DogProfileCreatedV1 now fires (moved from the old
/// CreateDogProfileHandler this wizard replaces) - Discovery only indexes
/// a dog once it's actually published, never a Draft still going through
/// the wizard. Requires Location to have been set by
/// AddDogProfileDetailsHandler first - not one of the yaml's own named
/// rejection outcomes, but a technical precondition: DogProfileCreatedV1
/// needs real coordinates for Discovery's proximity search, and Location
/// was already a required DogProfile field before this chapter existed.
///
/// Ownership-gated, same pattern as every other slice in this chapter.
/// </summary>
public static class PublishDogProfileHandler
{
    [WolverinePost("/api/v1/profiles/dogs/{dogProfileId:guid}/publish")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<(Results<Ok<PublishDogProfileResponse>, NotFound, ForbidHttpResult, Conflict<string>>, DogProfileCreatedV1?)> Handle(
        Guid dogProfileId,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var dogProfile = await session.LoadAsync<DogProfile>(dogProfileId, cancellationToken);
        if (dogProfile is null)
            return (TypedResults.NotFound(), null);

        if (dogProfile.OwnerId != callerOwnerId)
            return (TypedResults.Forbid(), null);

        if (dogProfile.Status != DogProfileStatus.Draft)
            return (TypedResults.Conflict($"Cannot publish a dog profile in status {dogProfile.Status}."), null);

        if (dogProfile.PhotoIds.Count == 0)
            return (TypedResults.Conflict("Publish Blocked: Photo Required."), null);

        if (dogProfile.Location is null)
            return (TypedResults.Conflict("Cannot publish a dog profile before its details have been added."), null);

        dogProfile.Publish();
        session.Store(dogProfile);
        await session.SaveChangesAsync(cancellationToken);

        var integrationEvent = new DogProfileCreatedV1(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            DogProfileId: dogProfile.Id,
            OwnerId: dogProfile.OwnerId,
            Breed: dogProfile.Breed,
            Latitude: dogProfile.Location.Latitude,
            Longitude: dogProfile.Location.Longitude);

        return (TypedResults.Ok(new PublishDogProfileResponse(dogProfile.Id, dogProfile.Status.ToString())), integrationEvent);
    }
}
