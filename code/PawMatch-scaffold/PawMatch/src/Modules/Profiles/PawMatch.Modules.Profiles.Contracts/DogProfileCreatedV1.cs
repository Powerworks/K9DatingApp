using PawMatch.BuildingBlocks.Domain;

namespace PawMatch.Modules.Profiles.Contracts;

/// <summary>
/// Published when a new dog profile is created. Consumed by Discovery
/// (to index the dog into the swipe pool). Versioned by name suffix -
/// if a breaking change is ever needed, add DogProfileCreatedV2 rather
/// than editing this one, so existing consumers keep working.
/// </summary>
public sealed record DogProfileCreatedV1(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid DogProfileId,
    Guid OwnerId,
    string Breed,
    double Latitude,
    double Longitude) : IIntegrationEvent;
