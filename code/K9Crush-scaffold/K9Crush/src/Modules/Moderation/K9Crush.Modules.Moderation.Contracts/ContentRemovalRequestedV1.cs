using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Moderation.Contracts;

/// <summary>
/// Published by Commands/RemoveContent - the emlang yaml's
/// ModeratingFlaggedContentUserReports chapter's "Remove Content" ->
/// "Content Removed". Moderation doesn't own the actual content (a
/// MediaAsset today, potentially a message/post/review once those
/// producer modules exist) so it can't delete it directly - it publishes
/// this instead, and whichever module owns that content type reacts (see
/// Media's RemoveMediaOnContentRemovalRequestedHandler).
///
/// ContentType is a plain string, not Moderation.Domain's own ContentType
/// enum - a Contracts project may only reference BuildingBlocks.Domain,
/// never its own module's Domain project, so the enum can't cross this
/// boundary. Consumers compare against the producer's own type name
/// (e.g. "Media") rather than sharing an enum value.
/// </summary>
public sealed record ContentRemovalRequestedV1(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid FlagId,
    string ContentType,
    Guid ContentId) : IIntegrationEvent;
