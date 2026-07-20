using K9Crush.Modules.Discovery.Domain.Events;

namespace K9Crush.Modules.Discovery.Api.Automations.DetectMutualMatch;

/// <summary>
/// Minimal command state for the DetectMutualMatch automation only (ADR-019
/// naming convention: [CommandName]State). Contains exactly the fields this
/// one decision needs - nothing more. Never persisted as a Marten
/// snapshot/document, and never referenced by any other command or
/// automation; if another handler ever needs similar-looking data, it
/// gets its own [CommandName]State type, not a reference to this one.
///
/// Built live per invocation via
/// <c>session.Events.AggregateStreamAsync&lt;DetectMutualMatchState&gt;(streamId)</c>
/// - Marten replays the stream through the Apply(...) methods below on
/// every call rather than reading a stored snapshot. For a two-event
/// stream (at most a handful of DogLiked/DogPassed plus one MatchFormed)
/// this is cheap; if a stream ever grew large enough for replay cost to
/// matter, the fix is a snapshot of THIS type specifically - still never
/// a shared bundle with other commands.
/// </summary>
public sealed class DetectMutualMatchState
{
    public Guid DogAId { get; private set; }
    public Guid DogBId { get; private set; }
    public bool DogALiked { get; private set; }
    public bool DogBLiked { get; private set; }
    public bool IsMatched { get; private set; }

    public void Apply(DogLiked e)
    {
        if (DogAId == Guid.Empty && DogBId == Guid.Empty)
        {
            (DogAId, DogBId) = e.SwiperDogId.CompareTo(e.TargetDogId) <= 0
                ? (e.SwiperDogId, e.TargetDogId)
                : (e.TargetDogId, e.SwiperDogId);
        }

        if (e.SwiperDogId == DogAId) DogALiked = true;
        else if (e.SwiperDogId == DogBId) DogBLiked = true;
    }

    public void Apply(MatchFormed e)
    {
        IsMatched = true;
    }
}
