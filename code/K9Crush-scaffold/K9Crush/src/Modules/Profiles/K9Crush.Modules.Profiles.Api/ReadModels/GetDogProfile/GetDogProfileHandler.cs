using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Profiles.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Profiles.Api.ReadModels.GetDogProfile;

public static class GetDogProfileHandler
{
    [WolverineGet("/api/v1/profiles/dogs/{dogProfileId:guid}")]
    public static async Task<Results<Ok<DogProfileResponse>, NotFound>> Handle(
        Guid dogProfileId,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var dog = await session.LoadAsync<DogProfile>(dogProfileId, cancellationToken);

        if (dog is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(new DogProfileResponse(
            dog.Id,
            dog.Status.ToString(),
            dog.Name,
            dog.Breed,
            dog.AgeInMonths,
            dog.Bio,
            dog.PhotoIds));
    }
}
