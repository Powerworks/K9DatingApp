using K9Crush.Modules.Chat.Domain.Events;

namespace K9Crush.Modules.Chat.Api.Commands.SendMessage;

/// <summary>
/// Minimal command state (ADR-019) for this command only - "does this
/// conversation exist, and who are its participants" - computed live via
/// AggregateStreamAsync, never persisted or shared with
/// MarkAsReadHandler's own (structurally similar but separate) state
/// type, per ADR-019's "no shared aggregate bundles" rule.
/// </summary>
public sealed class SendMessageState
{
    public bool Exists { get; private set; }
    public Guid OwnerAId { get; private set; }
    public Guid OwnerBId { get; private set; }

    public void Apply(ConversationCreated e)
    {
        Exists = true;
        OwnerAId = e.OwnerAId;
        OwnerBId = e.OwnerBId;
    }

    public bool HasParticipant(Guid ownerId) => ownerId == OwnerAId || ownerId == OwnerBId;
}
