namespace K9Crush.Modules.Profiles.Api.Commands.StartDogProfile;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record StartDogProfileResponse(Guid DogProfileId, string Status);
