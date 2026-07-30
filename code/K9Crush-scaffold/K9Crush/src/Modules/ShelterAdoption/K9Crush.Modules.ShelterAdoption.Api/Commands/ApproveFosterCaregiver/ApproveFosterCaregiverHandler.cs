using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.ApproveFosterCaregiver;

/// <summary>
/// State-change slice: Spec/K9CRUSH.emlang.v3.yaml's FosteringADog
/// chapter, "Approve Foster Caregiver" -> "Foster Caregiver Approved" -
/// only valid from UnderReview. Admin policy, no ownership check needed -
/// same reasoning as ReviewFosterApplicationHandler. Being an "approved
/// foster caregiver" is just having an Approved FosterApplication on file
/// - not a new OwnerRole, checked directly by PlaceDogInFosterHandler
/// rather than via a role/policy.
/// </summary>
public static class ApproveFosterCaregiverHandler
{
    [WolverinePost("/api/v1/shelter-adoption/foster-applications/{fosterApplicationId:guid}/approve")]
    [Authorize(Policy = "Admin")]
    public static async Task<Results<Ok<ApproveFosterCaregiverResponse>, NotFound, Conflict<string>>> Handle(
        Guid fosterApplicationId,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchForWriting<FosterApplication>(fosterApplicationId, cancellationToken);
        var fosterApplication = stream.Aggregate;
        if (fosterApplication is null)
            return TypedResults.NotFound();

        if (fosterApplication.Status != FosterApplicationStatus.UnderReview)
            return TypedResults.Conflict($"Cannot approve a foster application in status {fosterApplication.Status}.");

        var @event = fosterApplication.Approve();
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new ApproveFosterCaregiverResponse(fosterApplication.Id, fosterApplication.Status.ToString()));
    }
}
