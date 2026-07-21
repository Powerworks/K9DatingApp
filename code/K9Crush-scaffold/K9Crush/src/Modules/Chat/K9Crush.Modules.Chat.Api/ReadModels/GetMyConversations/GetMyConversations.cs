namespace K9Crush.Modules.Chat.Api.ReadModels.GetMyConversations;

public sealed record MyConversationEntry(Guid ConversationId, Guid OtherOwnerId, DateTimeOffset CreatedAt);

public sealed record MyConversationsResponse(IReadOnlyList<MyConversationEntry> Conversations);
