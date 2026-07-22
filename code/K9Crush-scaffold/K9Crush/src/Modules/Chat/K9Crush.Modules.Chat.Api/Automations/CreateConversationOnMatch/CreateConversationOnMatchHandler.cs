using Marten;
using K9Crush.Modules.Chat.Domain.Events;
using K9Crush.Modules.Discovery.Contracts;

namespace K9Crush.Modules.Chat.Api.Automations.CreateConversationOnMatch;

/// <summary>
/// Automation slice: EVENT(MatchCreatedV1, cross-module from Discovery)
/// -> AUTOMATION -> EVENT(ConversationCreated). Per
/// docs/05-event-modeling-blueprint.md's own slice table for this module
/// ("CreateConversationOnMatch (A) - MatchCreatedV1 -> ConversationCreated").
///
/// The conversation's stream id is Discovery's MatchId directly, not a
/// second pair-derived hash (Discovery.MatchStream.IdFor exists because
/// Discovery's swipe stream has no other natural shared key between two
/// dogs before a match forms; here MatchCreatedV1.MatchId is already a
/// stable, unique key for exactly this pair - reusing it avoids
/// redundant derivation).
///
/// Idempotency: redelivery of MatchCreatedV1 (at-least-once delivery)
/// must not create a duplicate conversation - guarded via
/// CreateConversationOnMatchState, same shape as DetectMutualMatchHandler's
/// isNewMutualMatch check.
/// </summary>
public static class CreateConversationOnMatchHandler
{
    public static async Task Handle(
        MatchCreatedV1 integrationEvent,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var conversationId = integrationEvent.MatchId;

        var state = await session.Events.AggregateStreamAsync<CreateConversationOnMatchState>(
            conversationId, token: cancellationToken);

        if (state is { Exists: true })
            return; // already created - redelivered event, no-op

        session.Events.Append(conversationId, new ConversationCreated(
            conversationId,
            integrationEvent.OwnerAId,
            integrationEvent.OwnerBId,
            integrationEvent.MatchId,
            DateTimeOffset.UtcNow));

        await session.SaveChangesAsync(cancellationToken);
    }
}
