namespace PawMatch.BuildingBlocks.Domain;

/// <summary>
/// Marks a type as a Marten domain event, i.e. something appended to an
/// event-sourced aggregate's stream (Discovery/Matching, Chat). Purely
/// internal to the module that owns the aggregate.
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// Marks a type as a cross-module integration event: the only kind of
/// payload one module's Contracts project is allowed to expose, and the
/// only kind of message Wolverine is allowed to route across the
/// pawmatch.events RabbitMQ exchange. Integration events are versioned by
/// name (e.g. MatchCreatedV1) and must be additive/backwards compatible.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}
