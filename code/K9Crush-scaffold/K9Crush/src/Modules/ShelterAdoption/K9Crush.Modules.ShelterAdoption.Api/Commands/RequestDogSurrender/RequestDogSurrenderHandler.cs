using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.RequestDogSurrender;

/// <summary>
/// State-change slice: Spec/K9CRUSH.emlang.v3.yaml's SurrenderingYourDog
/// chapter, "Request Dog Surrender" -> "Dog Surrender Requested" - a
/// member surrendering their OWN dog into a shelter's care, distinct from
/// TheWouldBeAdopter's applicant journey. Any verified owner can request -
/// no Shelter/Admin role needed, same reasoning as SubmitApplicationHandler.
/// </summary>
public static class RequestDogSurrenderHandler
{
    [WolverinePost("/api/v1/shelter-adoption/surrender-requests")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Ok<RequestDogSurrenderResponse>> Handle(
        RequestDogSurrenderRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var requestedByOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var surrenderRequest = DogSurrenderRequest.Request(
            requestedByOwnerId, request.DogName, request.Breed, request.AgeInMonths,
            request.ReasonForSurrender, request.TemperamentNotes, request.HealthNotes);
        session.Store(surrenderRequest);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new RequestDogSurrenderResponse(surrenderRequest.Id));
    }
}
