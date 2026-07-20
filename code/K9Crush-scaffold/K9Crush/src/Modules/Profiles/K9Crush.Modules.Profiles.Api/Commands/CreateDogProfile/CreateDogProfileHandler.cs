using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using K9Crush.Modules.Profiles.Contracts;
using K9Crush.Modules.Profiles.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Profiles.Api.Commands.CreateDogProfile;

public static class CreateDogProfileHandler
{
    /// <summary>
    /// Route + validation (via CreateDogProfileValidator, run automatically
    /// by Wolverine's FluentValidation middleware) + handler logic all live
    /// in this one file - that's the "vertical slice" in practice.
    ///
    /// The return tuple's second item (DogProfileCreatedV1) is a Wolverine
    /// "cascading message": Wolverine publishes it after the handler
    /// returns, enlisted in the same Marten transaction via the
    /// WolverineFx.Marten outbox integration - so the event is guaranteed
    /// to be published if and only if the DogProfile was actually saved.
    ///
    /// [Authorize] was missing entirely in the original scaffold - an
    /// anonymous request reached the handler, ClaimsPrincipal had no
    /// NameIdentifier claim, and Guid.Parse(null!) threw an unhandled
    /// exception instead of the request being rejected with a clean 401
    /// by the authorization middleware. Confirm Wolverine.Http honors a
    /// plain [Authorize] attribute on the handler method the same way
    /// minimal APIs do (it's designed to be idiomatic with ASP.NET Core
    /// endpoint conventions, but this specific attribute placement is
    /// unverified against a running instance) - if the request still
    /// reaches this far without a token after this change, that's the
    /// thing to check next.
    /// </summary>
    [WolverinePost("/api/v1/profiles/dogs")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<(CreateDogProfileResponse, DogProfileCreatedV1)> Handle(
        CreateDogProfileRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var ownerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var location = GeoCoordinate.Create(request.Latitude, request.Longitude);

        var dogProfile = DogProfile.Create(
            ownerId,
            request.Name,
            request.Breed,
            request.AgeInMonths,
            request.Bio,
            location);

        session.Store(dogProfile);
        await session.SaveChangesAsync(cancellationToken);

        var response = new CreateDogProfileResponse(dogProfile.Id);

        var integrationEvent = new DogProfileCreatedV1(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            DogProfileId: dogProfile.Id,
            OwnerId: ownerId,
            Breed: request.Breed,
            Latitude: request.Latitude,
            Longitude: request.Longitude);

        return (response, integrationEvent);
    }
}
