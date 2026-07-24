using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.ApplyToFoster;

/// <summary>
/// State-change slice: Spec/K9CRUSH.emlang.v3.yaml's FosteringADog
/// chapter, "Apply To Foster" -> "Foster Application Submitted" - a
/// member applying to become an approved foster caregiver, before any
/// specific dog is involved. Any verified owner can apply - no
/// Shelter/Admin role needed, same reasoning as RequestDogSurrenderHandler.
/// </summary>
public static class ApplyToFosterHandler
{
    [WolverinePost("/api/v1/shelter-adoption/foster-applications")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Ok<ApplyToFosterResponse>> Handle(
        ApplyToFosterRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var applicantOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var (fosterApplication, @event) = FosterApplication.ApplyNew(
            applicantOwnerId, request.HomeType, request.HasGarden, request.HasOtherPets, request.AvailableFrom);
        session.Events.StartStream<FosterApplication>(fosterApplication.Id, @event);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new ApplyToFosterResponse(fosterApplication.Id));
    }
}
