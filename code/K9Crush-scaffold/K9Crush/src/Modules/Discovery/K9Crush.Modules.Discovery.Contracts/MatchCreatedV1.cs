using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Discovery.Contracts;

/// <summary>
/// Published when two dogs' owners mutually like each other's dogs.
/// Consumed by Chat (creates an empty Conversation) and Notifications
/// (alerts both owners).
/// </summary>
public sealed record MatchCreatedV1(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid MatchId,
    Guid DogAId,
    Guid DogBId) : IIntegrationEvent;
