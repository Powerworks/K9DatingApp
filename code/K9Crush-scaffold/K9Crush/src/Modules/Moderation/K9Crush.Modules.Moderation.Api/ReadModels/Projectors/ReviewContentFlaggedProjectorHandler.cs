using Marten;
using K9Crush.Modules.Moderation.Domain;
using K9Crush.Modules.Places.Contracts;

namespace K9Crush.Modules.Moderation.Api.ReadModels.Projectors;

/// <summary>
/// The "EVENT -> READMODEL" half of the Moderation Queue/Flagged Content
/// Detail state-views, triggered by Places' cross-module
/// ReviewContentFlaggedV1 - the second real "Content Flagged" producer
/// after Media's MediaContentFlaggedProjectorHandler (see that handler's
/// own doc comment for the fuller writeup, not repeated here).
/// </summary>
public static class ReviewContentFlaggedProjectorHandler
{
    public static async Task Handle(ReviewContentFlaggedV1 integrationEvent, IDocumentSession session, CancellationToken cancellationToken)
    {
        session.Store(FlaggedContent.Create(
            ContentType.Review,
            integrationEvent.ReviewId,
            integrationEvent.ContentOwnerId,
            integrationEvent.ReporterOwnerId,
            integrationEvent.OccurredAt));

        await session.SaveChangesAsync(cancellationToken);
    }
}
