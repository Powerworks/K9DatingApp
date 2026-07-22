using Marten;
using K9Crush.Modules.Media.Contracts;
using K9Crush.Modules.Moderation.Domain;

namespace K9Crush.Modules.Moderation.Api.ReadModels.Projectors;

/// <summary>
/// The "EVENT -> READMODEL" half of the Moderation Queue/Flagged Content
/// Detail state-views (see Event Modeling blueprint, Section 4) - keeps
/// FlaggedContent current so GetModerationQueueHandler/
/// GetFlaggedContentDetailHandler never look past a plain document query.
/// Triggered by Media's cross-module MediaContentFlaggedV1 over RabbitMQ,
/// same mechanism as Admin's FeedbackSubmittedProjectorHandler.
///
/// Store() is an upsert keyed by a fresh Id (not the incoming MediaAssetId -
/// unlike Admin's FeedbackInboxItem, a single piece of content could
/// plausibly be reported more than once and each report deserves its own
/// queue entry, not a silent overwrite), so this always creates rather
/// than upserts onto an existing document. Explicitly calls
/// SaveChangesAsync (easy to forget, see that handler's own doc comment
/// for the bug this caused once elsewhere in this codebase).
/// </summary>
public static class MediaContentFlaggedProjectorHandler
{
    public static async Task Handle(MediaContentFlaggedV1 integrationEvent, IDocumentSession session, CancellationToken cancellationToken)
    {
        session.Store(FlaggedContent.Create(
            ContentType.Media,
            integrationEvent.MediaAssetId,
            integrationEvent.ContentOwnerId,
            integrationEvent.ReporterOwnerId,
            integrationEvent.OccurredAt));

        await session.SaveChangesAsync(cancellationToken);
    }
}
