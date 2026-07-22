using K9Crush.Modules.Discovery.Domain.Events;

namespace K9Crush.Modules.Discovery.Api.Commands.UndoLastSwipe;

/// <summary>
/// Minimal command state for the UndoLastSwipe command only (ADR-019
/// naming convention: [CommandName]State). Tracks, per side of the pair
/// stream, whether that side currently has an active (not-yet-undone)
/// swipe recorded - nothing more. Computed live per invocation via
/// <c>session.Events.AggregateStreamAsync&lt;UndoLastSwipeState&gt;(streamId)</c>,
/// never persisted, never shared with DetectMutualMatchState or any other
/// handler even though the shape looks similar - see that type's own doc
/// comment for why two commands needing "similar-looking" state still get
/// two separate types.
/// </summary>
public sealed class UndoLastSwipeState
{
    public Guid DogAId { get; private set; }
    public Guid DogBId { get; private set; }
    private bool _dogASwipeActive;
    private bool _dogBSwipeActive;

    public bool HasActiveSwipeFor(Guid dogId) =>
        dogId == DogAId ? _dogASwipeActive : dogId == DogBId && _dogBSwipeActive;

    public void Apply(DogLiked e) => RecordSwipe(e.SwiperDogId, e.TargetDogId);

    public void Apply(DogPassed e) => RecordSwipe(e.SwiperDogId, e.TargetDogId);

    public void Apply(SwipeUndone e)
    {
        if (e.SwiperDogId == DogAId) _dogASwipeActive = false;
        else if (e.SwiperDogId == DogBId) _dogBSwipeActive = false;
    }

    private void RecordSwipe(Guid swiperDogId, Guid targetDogId)
    {
        if (DogAId == Guid.Empty && DogBId == Guid.Empty)
        {
            (DogAId, DogBId) = swiperDogId.CompareTo(targetDogId) <= 0
                ? (swiperDogId, targetDogId)
                : (targetDogId, swiperDogId);
        }

        if (swiperDogId == DogAId) _dogASwipeActive = true;
        else if (swiperDogId == DogBId) _dogBSwipeActive = true;
    }
}
