namespace K9Crush.Modules.Chat.Domain;

/// <summary>
/// Read-model document, kept up to date by ConversationCreatedProjectorHandler
/// reacting to the same-module ConversationCreated domain event (Marten
/// forwarding, same mechanism Discovery uses for DogLiked). Exists so
/// GetConversationHistoryHandler/GetMyConversationsHandler never have to
/// replay the event stream on the read path - same "EVENT -> READMODEL"
/// split as Discovery's DiscoveryFeedItem.
///
/// No participant display names - OwnerAId/OwnerBId are raw ids. Chat
/// can't reach into Identity's Domain (Contracts-only cross-module
/// boundary), and resolving names would need its own OwnerContact-style
/// consumer of Identity's OwnerRegisteredV1 (same pattern Notifications
/// already built) - a straightforward, deliberately deferred follow-up,
/// not attempted in this first increment to keep scope to what was asked.
/// </summary>
public class ConversationSummary
{
    public Guid Id { get; set; } // same as the stream id (Discovery's MatchId)
    public Guid OwnerAId { get; set; }
    public Guid OwnerBId { get; set; }
    public Guid MatchId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
