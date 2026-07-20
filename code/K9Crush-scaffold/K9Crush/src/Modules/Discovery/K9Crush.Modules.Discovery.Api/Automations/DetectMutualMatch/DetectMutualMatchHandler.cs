using Marten;
using K9Crush.Modules.Discovery.Contracts;
using K9Crush.Modules.Discovery.Domain;
using K9Crush.Modules.Discovery.Domain.Events;

namespace K9Crush.Modules.Discovery.Api.Automations.DetectMutualMatch;

/// <summary>
/// Automation slice: EVENT(DogLiked) -> AUTOMATION -> COMMAND(append
/// MatchFormed) -> EVENT(s).
///
/// Per ADR-019, this loads its OWN minimal command state
/// (DetectMutualMatchState) computed live from the stream - not a shared
/// MatchAggregate snapshot. If a second automation or command ever needs
/// to reason about this stream, it gets its own [CommandName]State type,
/// never a reference to this one.
///
/// Wired via Marten event forwarding: DiscoveryModule's Marten
/// configuration enables `.IntegrateWithWolverine(w =>
/// w.SubscribeToEvent&lt;DogLiked&gt;())`, which routes every captured
/// DogLiked event to this local Wolverine handler after the originating
/// transaction commits. Confirm the exact forwarding/subscription API
/// against the installed WolverineFx.Marten version when implementing -
/// this has evolved across Wolverine releases (event forwarding vs. the
/// newer async-daemon-driven event subscriptions); either mechanism
/// satisfies this slice's contract of "runs once per DogLiked event."
///
/// The cascaded MatchCreatedV1 is published through the same durable
/// outbox as every other integration event, so Chat/Notifications never
/// see a match that didn't actually get persisted.
/// </summary>
public static class DetectMutualMatchHandler
{
    public static async Task<MatchCreatedV1?> Handle(
        DogLiked domainEvent,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var streamId = MatchStream.IdFor(domainEvent.SwiperDogId, domainEvent.TargetDogId);

        // Live aggregation: replays the stream through DetectMutualMatchState's
        // Apply(...) methods on every call. Nothing is persisted here - this
        // is the mechanical difference from the old LoadAsync<MatchAggregate>
        // approach, which read a stored, shared snapshot.
        var state = await session.Events.AggregateStreamAsync<DetectMutualMatchState>(
            streamId, token: cancellationToken);

        var isNewMutualMatch = state is { DogALiked: true, DogBLiked: true, IsMatched: false };
        if (!isNewMutualMatch)
            return null; // nothing to do - no cascaded message published

        var now = DateTimeOffset.UtcNow;
        session.Events.Append(streamId, new MatchFormed(state!.DogAId, state.DogBId, now));
        await session.SaveChangesAsync(cancellationToken);

        return new MatchCreatedV1(
            EventId: Guid.NewGuid(),
            OccurredAt: now,
            MatchId: streamId,
            DogAId: state.DogAId,
            DogBId: state.DogBId);
    }
}
