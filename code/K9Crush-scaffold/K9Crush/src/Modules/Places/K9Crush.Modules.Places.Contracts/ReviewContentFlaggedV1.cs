using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Places.Contracts;

/// <summary>
/// Published by Commands/ReportReview - the emlang yaml's
/// LeaveAReviewRestaurantOrDogPark chapter's "Report Review" -> "Content
/// Flagged". Same shape and reasoning as Media's MediaContentFlaggedV1 -
/// see that contract's own doc comment for why "Content Flagged" gets a
/// distinctly-named event per producer module rather than one shared
/// type. Consumed by Moderation (ReadModels/Projectors/
/// ReviewContentFlaggedProjectorHandler), the second real producer after
/// Media, confirming the "one queue, many distinctly-named triggers"
/// design actually generalizes.
/// </summary>
public sealed record ReviewContentFlaggedV1(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid ReviewId,
    Guid ContentOwnerId,
    Guid ReporterOwnerId) : IIntegrationEvent;
