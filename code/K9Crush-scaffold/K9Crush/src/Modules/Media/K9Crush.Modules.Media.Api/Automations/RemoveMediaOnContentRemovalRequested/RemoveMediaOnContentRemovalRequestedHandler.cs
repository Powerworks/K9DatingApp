using Marten;
using K9Crush.Modules.Media.Domain;
using K9Crush.Modules.Moderation.Contracts;

namespace K9Crush.Modules.Media.Api.Automations.RemoveMediaOnContentRemovalRequested;

/// <summary>
/// Automation slice: EVENT(ContentRemovalRequestedV1, cross-module via
/// RabbitMQ) -> AUTOMATION -> deletes the MediaAsset. The other half of
/// Moderation's "Remove Content" command - Moderation doesn't own this
/// document so it can't delete it directly, it publishes this event and
/// Media reacts. Filters to ContentType == "Media" (a plain string on
/// the wire, not a shared enum - see the contract's own doc comment) since
/// this same event could eventually target other content types once
/// their producer modules exist; anything else is silently ignored, not
/// an error, since a message with no matching handler logic here is
/// exactly as valid as one this module was never meant to act on.
///
/// Idempotent: deleting an already-deleted (or never-existed) MediaAsset
/// is a no-op, same as every other automation reacting to an
/// at-least-once delivered event in this codebase.
/// </summary>
public static class RemoveMediaOnContentRemovalRequestedHandler
{
    public static async Task Handle(ContentRemovalRequestedV1 integrationEvent, IDocumentSession session, CancellationToken cancellationToken)
    {
        // ContentType on the wire is Moderation.Domain's ContentType enum
        // rendered ToString() ("Media"), not this module's own MediaType
        // enum (Photo/Video) - compared as a literal, not reusing MediaType,
        // to avoid confusing two same-shaped-looking but unrelated enums.
        if (integrationEvent.ContentType != "Media")
            return;

        var mediaAsset = await session.LoadAsync<MediaAsset>(integrationEvent.ContentId, cancellationToken);
        if (mediaAsset is null)
            return;

        session.Delete(mediaAsset);
        await session.SaveChangesAsync(cancellationToken);
    }
}
