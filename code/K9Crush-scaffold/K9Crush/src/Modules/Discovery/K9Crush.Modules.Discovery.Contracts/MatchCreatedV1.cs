using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Discovery.Contracts;

/// <summary>
/// Published when two dogs' owners mutually like each other's dogs.
/// Consumed by Chat (creates an empty Conversation) and Notifications
/// (alerts both owners - NotifyOnMatchHandler).
///
/// OwnerAId/OwnerBId were added alongside NotifyOnMatchHandler - this
/// record's own doc comment already said "alerts both owners" but never
/// actually carried an owner id, only dog ids. DetectMutualMatchHandler
/// resolves them via its own module's DiscoveryFeedItem (same-module
/// document read, not a boundary violation) before publishing.
/// </summary>
public sealed record MatchCreatedV1(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid MatchId,
    Guid DogAId,
    Guid DogBId,
    Guid OwnerAId,
    Guid OwnerBId) : IIntegrationEvent;
