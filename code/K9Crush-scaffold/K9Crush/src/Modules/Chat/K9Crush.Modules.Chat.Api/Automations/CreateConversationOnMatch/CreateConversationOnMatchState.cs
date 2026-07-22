using K9Crush.Modules.Chat.Domain.Events;

namespace K9Crush.Modules.Chat.Api.Automations.CreateConversationOnMatch;

/// <summary>
/// Minimal command state (ADR-019 naming convention) for this automation
/// only - the idempotency guard against a redelivered MatchCreatedV1
/// creating a duplicate conversation. Computed live per invocation via
/// AggregateStreamAsync, never persisted or referenced by any other
/// handler - same discipline as Discovery's DetectMutualMatchState.
/// </summary>
public sealed class CreateConversationOnMatchState
{
    public bool Exists { get; private set; }

    public void Apply(ConversationCreated e) => Exists = true;
}
