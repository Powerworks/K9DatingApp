namespace K9Crush.Modules.Chat.Api.ReadModels.GetConversationHistory;

public sealed record ChatMessageEntry(Guid MessageId, Guid SenderOwnerId, string Text, DateTimeOffset SentAt);

public sealed record ConversationHistoryResponse(
    Guid ConversationId,
    Guid OwnerAId,
    Guid OwnerBId,
    IReadOnlyList<ChatMessageEntry> Messages);
