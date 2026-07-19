namespace PawMatch.Modules.Profiles.Api.ReadModels.GetDogProfile;

public sealed record DogProfileResponse(
    Guid DogProfileId,
    string Name,
    string Breed,
    int AgeInMonths,
    string Bio,
    IReadOnlyList<Guid> PhotoIds);
