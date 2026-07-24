using Marten;
using K9Crush.Modules.Admin.Domain;
using K9Crush.Modules.Identity.Contracts;

namespace K9Crush.Modules.Admin.Api.ReadModels.Projectors;

/// <summary>
/// The "EVENT -> READMODEL" half of the Feedback Inbox/Feedback Detail
/// state-views (see Event Modeling blueprint, Section 4) - keeps
/// FeedbackInboxItem current so GetFeedbackInboxHandler/
/// GetFeedbackDetailHandler never look past a plain document query.
/// Triggered by Identity's cross-module FeedbackSubmittedV1 over
/// RabbitMQ, same mechanism as Discovery's DogProfileCreatedProjectorHandler.
///
/// ADR-031: this used to be a Store() upsert keyed by Id, safe against
/// at-least-once redelivery for free. Event streams don't upsert -
/// StartStream on an id that already has a stream throws
/// ExistingStreamIdCollisionException - so redelivery safety now needs an
/// explicit existence check first. AggregateStreamAsync (not
/// FetchForWriting) is enough here since this handler only decides
/// "does a stream already exist," never appends to one that does.
/// </summary>
public static class FeedbackSubmittedProjectorHandler
{
    public static async Task Handle(FeedbackSubmittedV1 integrationEvent, IDocumentSession session, CancellationToken cancellationToken)
    {
        var existing = await session.Events.AggregateStreamAsync<FeedbackInboxItem>(integrationEvent.FeedbackId, token: cancellationToken);
        if (existing is not null)
            return;

        var (_, @event) = FeedbackInboxItem.CreateNew(
            integrationEvent.FeedbackId,
            integrationEvent.OwnerId,
            integrationEvent.Message,
            integrationEvent.SubmittedAt);

        session.Events.StartStream<FeedbackInboxItem>(integrationEvent.FeedbackId, @event);
        await session.SaveChangesAsync(cancellationToken);
    }
}
