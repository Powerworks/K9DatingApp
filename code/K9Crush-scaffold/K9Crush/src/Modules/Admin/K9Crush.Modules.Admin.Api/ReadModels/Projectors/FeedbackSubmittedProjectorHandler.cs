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
/// Store() is an upsert keyed by Id (set to FeedbackId), so at-least-once
/// redelivery is safe - explicitly calls SaveChangesAsync (easy to forget,
/// see that handler's own doc comment for the bug this caused once
/// elsewhere in this codebase).
/// </summary>
public static class FeedbackSubmittedProjectorHandler
{
    public static async Task Handle(FeedbackSubmittedV1 integrationEvent, IDocumentSession session, CancellationToken cancellationToken)
    {
        session.Store(FeedbackInboxItem.Create(
            integrationEvent.FeedbackId,
            integrationEvent.OwnerId,
            integrationEvent.Message,
            integrationEvent.SubmittedAt));

        await session.SaveChangesAsync(cancellationToken);
    }
}
