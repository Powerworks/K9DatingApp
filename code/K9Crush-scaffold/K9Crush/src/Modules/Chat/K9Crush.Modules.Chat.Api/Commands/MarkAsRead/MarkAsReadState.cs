using K9Crush.Modules.Chat.Domain.Events;

namespace K9Crush.Modules.Chat.Api.Commands.MarkAsRead;

/// <summary>
/// Minimal command state (ADR-019) for this command only - structurally
/// identical to SendMessageState, but kept as its own type per ADR-019's
/// "a second command needing similar-looking data gets its own
/// [CommandName]State, never a reference to the first one" rule.
/// </summary>
public sealed class MarkAsReadState
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
