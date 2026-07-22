using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Media.Contracts;

/// <summary>
/// Published by Commands/ReportMedia - the emlang yaml's
/// UploadShareRemovePhotosAndVideos chapter's "Report Media" ->
/// "Content Flagged". "Content Flagged" is a genuinely shared event name
/// across five different chapters in the yaml (this one, MessagingDirectGroup's
/// Report Message, ActivityFeed's Report Post, LeaveAReviewRestaurantOrDogPark's
/// Report Review) - see module_boundaries memory's note that this belongs
/// to a future Moderation module. Each producing module publishes its own
/// distinctly-named event (MediaContentFlaggedV1 here) rather than a
/// shared generic type, so Moderation can build one read model off
/// several distinct triggers later, the same way Notifications consumes
/// several distinctly-named ShelterAdoption events into one dispatcher.
///
/// ContentOwnerId (the MediaAsset's uploader - who Warn/Suspend/Ban
/// actions target) was added when the Moderation module was actually
/// built and needed it - the original shape only carried ReporterOwnerId
/// (who filed the report), which isn't enough for a real moderation
/// workflow that needs to know who to act against.
/// </summary>
public sealed record MediaContentFlaggedV1(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid MediaAssetId,
    Guid ContentOwnerId,
    Guid ReporterOwnerId) : IIntegrationEvent;
