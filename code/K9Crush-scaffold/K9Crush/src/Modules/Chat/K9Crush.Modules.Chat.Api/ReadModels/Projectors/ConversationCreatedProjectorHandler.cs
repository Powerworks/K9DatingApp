using Marten;
using K9Crush.Modules.Chat.Domain;
using K9Crush.Modules.Chat.Domain.Events;

namespace K9Crush.Modules.Chat.Api.ReadModels.Projectors;

/// <summary>
/// The "EVENT -> READMODEL" half of the GetConversationHistory/
/// GetMyConversations state-view slices (see Event Modeling blueprint,
/// Section 4) - keeps ConversationSummary current so those handlers never
/// have to replay the event stream on the read path. Triggered by the
/// same-module domain event ConversationCreated via Marten forwarding
/// (Api.Host/Program.cs's SubscribeToEvent&lt;ConversationCreated&gt;()),
/// same mechanism as Discovery's DogLiked -> DetectMutualMatchHandler.
///
/// Delivery is at-least-once; Store() is an upsert keyed by Id, so
/// redelivery is safe without extra guarding.
/// </summary>
public static class ConversationCreatedProjectorHandler
{
    public static async Task Handle(ConversationCreated domainEvent, IDocumentSession session, CancellationToken cancellationToken)
    {
        session.Store(new ConversationSummary
        {
            Id = domainEvent.ConversationId,
            OwnerAId = domainEvent.OwnerAId,
            OwnerBId = domainEvent.OwnerBId,
            MatchId = domainEvent.MatchId,
            CreatedAt = domainEvent.OccurredAt
        });

        await session.SaveChangesAsync(cancellationToken);
    }
}
