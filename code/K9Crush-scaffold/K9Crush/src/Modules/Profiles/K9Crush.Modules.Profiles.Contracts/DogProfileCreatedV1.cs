using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Profiles.Contracts;

/// <summary>
/// Published when a dog profile is actually published (AddDogProfile
/// chapter's "Publish Dog Profile" step - PublishDogProfileHandler), not
/// merely started - a Draft still going through the wizard is never
/// Discovery-visible. Consumed by Discovery (to index the dog into the
/// swipe pool). Versioned by name suffix - if a breaking change is ever
/// needed, add DogProfileCreatedV2 rather than editing this one, so
/// existing consumers keep working.
/// </summary>
public sealed record DogProfileCreatedV1(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid DogProfileId,
    Guid OwnerId,
    string Name,
    string Breed,
    double Latitude,
    double Longitude) : IIntegrationEvent;
