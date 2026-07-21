namespace K9Crush.Modules.Profiles.Api.Commands.PublishDogProfile;

/// <summary>What this slice hands back to the caller.</summary>
public sealed record PublishDogProfileResponse(Guid DogProfileId, string Status);
