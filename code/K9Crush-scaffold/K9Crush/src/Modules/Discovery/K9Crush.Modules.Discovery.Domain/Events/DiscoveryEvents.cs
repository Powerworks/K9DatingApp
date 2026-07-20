using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Discovery.Domain.Events;

/// <summary>
/// Appended when a dog's owner swipes right on another dog. This is the
/// raw fact - "what happened" - which is exactly why this module is
/// event-sourced rather than document-based (see Solution Architecture
/// doc, Section 4): match history and swipe provenance are first-class
/// data here, not just current state.
/// </summary>
public sealed record DogLiked(Guid SwiperDogId, Guid TargetDogId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record DogPassed(Guid SwiperDogId, Guid TargetDogId, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// Appended to the shared pair-stream (see MatchStream.IdFor) when a
/// reverse-like is detected.
/// </summary>
public sealed record MatchFormed(Guid DogAId, Guid DogBId, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// Appended when an owner undoes their own most recent swipe (like or
/// pass) against TargetDogId - the "oops, wrong swipe" case. Only
/// reverses the caller's own side of the pair stream; see
/// Commands/UndoLastSwipe/UndoLastSwipeState.cs for how "active swipe" is
/// tracked per side.
/// </summary>
public sealed record SwipeUndone(Guid SwiperDogId, Guid TargetDogId, DateTimeOffset OccurredAt) : IDomainEvent;
