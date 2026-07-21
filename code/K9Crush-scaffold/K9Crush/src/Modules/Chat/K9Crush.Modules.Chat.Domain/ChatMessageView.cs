namespace K9Crush.Modules.Chat.Domain;

/// <summary>
/// Read-model document, kept up to date by MessageSentProjectorHandler
/// reacting to the same-module MessageSent domain event - same
/// "EVENT -> READMODEL" split as ConversationSummary/DiscoveryFeedItem.
/// </summary>
public class ChatMessageView
{
    public Guid Id { get; set; } // same as MessageSent.MessageId
    public Guid ConversationId { get; set; }
    public Guid SenderOwnerId { get; set; }
    public string Text { get; set; } = default!;
    public DateTimeOffset SentAt { get; set; }
}
