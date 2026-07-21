using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Profiles.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Profiles.Api.Commands.StartDogProfile;

/// <summary>
/// State-change slice: the emlang yaml's AddDogProfile chapter's "Start
/// Dog Profile" -> "Dog Profile Started" (maxDogProfiles: 10). First step
/// of the wizard this slice replaces the old single-shot CreateDogProfile
/// with - see DogProfile.cs's Start() doc comment.
/// </summary>
public static class StartDogProfileHandler
{
    private const int MaxDogProfiles = 10;

    [WolverinePost("/api/v1/profiles/dogs")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<StartDogProfileResponse>, Conflict<string>>> Handle(
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var ownerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var existingCount = await session.Query<DogProfile>()
            .CountAsync(x => x.OwnerId == ownerId, cancellationToken);
        if (existingCount >= MaxDogProfiles)
            return TypedResults.Conflict($"Dog profile limit reached - at most {MaxDogProfiles} dog profiles allowed.");

        var dogProfile = DogProfile.Start(ownerId);
        session.Store(dogProfile);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new StartDogProfileResponse(dogProfile.Id, dogProfile.Status.ToString()));
    }
}
