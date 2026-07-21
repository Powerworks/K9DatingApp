using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Chat.Domain.Events;

/// <summary>
/// Appended once per Conversation stream, always the first event -
/// Automations/CreateConversationOnMatch. The conversation's own stream
/// id is Discovery's MatchId directly (see that handler's doc comment
/// for why no second id-derivation is needed here, unlike
/// Discovery.MatchStream.IdFor's pair-hash for a stream that doesn't
/// already have a natural shared key).
/// </summary>
public sealed record ConversationCreated(
    Guid ConversationId, Guid OwnerAId, Guid OwnerBId, Guid MatchId, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// Appended by SendMessageHandler. Text is the raw message content -
/// this module is event-sourced (full message history, per
/// docs/03-solution-architecture.md Section 2.1), so the event itself is
/// the durable record, not just a read-model row.
/// </summary>
public sealed record MessageSent(
    Guid ConversationId, Guid MessageId, Guid SenderOwnerId, string Text, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// Appended by MarkAsReadHandler - the reader's read-cursor advancing to
/// LastReadMessageId, not a per-message read-receipt toggle (matches how
/// most chat UIs actually work: "read up to here", not individually
/// flagged messages). This increment doesn't project read state into any
/// read model yet (no read-receipt UI need identified for the first
/// pass) - the event is still recorded for the full history event
/// sourcing is meant to preserve, even though nothing reads it back yet.
/// </summary>
public sealed record MessageRead(
    Guid ConversationId, Guid ReaderOwnerId, Guid LastReadMessageId, DateTimeOffset OccurredAt) : IDomainEvent;
