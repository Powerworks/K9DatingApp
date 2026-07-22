using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Discovery.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Discovery.Api.Commands.FlagDogOfInterest;

/// <summary>
/// State-change slice: TheWindowShopper chapter's "Flag Dog Of Interest"
/// -> "Dog Of Interest Flagged" - any signed-in member bookmarking a dog
/// from the feed, no precondition beyond the dog still existing (matches
/// the yaml's own test, whose only given is "Nearby Dogs Previewed").
///
/// Shares its actual mechanism (DogOfInterest.Flag) with
/// ClaimSavedMatchHandler - see DogOfInterest.cs's doc comment for why
/// these are the same underlying action under two narrative framings,
/// not two separate features.
/// </summary>
public static class FlagDogOfInterestHandler
{
    [WolverinePost("/api/v1/discovery/dogs/{dogId:guid}/flag-interest")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<FlagDogOfInterestResponse>, Conflict<string>>> Handle(
        Guid dogId,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var ownerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var dog = await session.LoadAsync<DiscoveryFeedItem>(dogId, cancellationToken);
        if (dog is null)
            return TypedResults.Conflict("Saved Match No Longer Available.");

        var dogOfInterest = DogOfInterest.Flag(ownerId, dogId);
        session.Store(dogOfInterest);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new FlagDogOfInterestResponse(dogId));
    }
}
