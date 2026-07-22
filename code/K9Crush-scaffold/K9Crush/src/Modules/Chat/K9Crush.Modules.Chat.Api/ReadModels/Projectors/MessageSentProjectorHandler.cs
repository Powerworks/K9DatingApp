using Marten;
using K9Crush.Modules.Chat.Domain;
using K9Crush.Modules.Chat.Domain.Events;

namespace K9Crush.Modules.Chat.Api.ReadModels.Projectors;

/// <summary>
/// The "EVENT -> READMODEL" half of GetConversationHistoryHandler's
/// message list - keeps ChatMessageView current, same pattern as
/// ConversationCreatedProjectorHandler. Triggered by the same-module
/// domain event MessageSent via Marten forwarding.
/// </summary>
public static class MessageSentProjectorHandler
{
    public static async Task Handle(MessageSent domainEvent, IDocumentSession session, CancellationToken cancellationToken)
    {
        session.Store(new ChatMessageView
        {
            Id = domainEvent.MessageId,
            ConversationId = domainEvent.ConversationId,
            SenderOwnerId = domainEvent.SenderOwnerId,
            Text = domainEvent.Text,
            SentAt = domainEvent.OccurredAt
        });

        await session.SaveChangesAsync(cancellationToken);
    }
}
