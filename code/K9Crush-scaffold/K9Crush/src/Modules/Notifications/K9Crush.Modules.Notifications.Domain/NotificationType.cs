namespace K9Crush.Modules.Notifications.Domain;

/// <summary>
/// The emlang yaml's ManagingNotificationPreferences chapter lists exactly
/// four categories in its notificationType prop (application_status,
/// messages, playdate_requests, activity_feed). Matches is a fifth value
/// this codebase adds beyond that list - the yaml's own chapter is silent
/// on it, but HLD (docs/04-high-level-design.md Section 1.5) and the
/// Event Modeling blueprint (docs/05, Full Slice Inventory) both name
/// NotifyOnMatch/MatchCreatedV1 as the headline Notifications example, and
/// NotifyOnMatch is this module's first real automation - it needs a
/// category to check preferences against. Treated as a genuine gap in the
/// yaml's enum, not a silent override of it.
///
/// Serialized as its integer ordinal (Marten/System.Text.Json, same as
/// every other enum in this codebase - see ApplicationStatus.cs). Append
/// new members at the end, never reorder.
/// </summary>
public enum NotificationType
{
    ApplicationStatus,
    Messages,
    PlaydateRequests,
    ActivityFeed,
    Matches
}
