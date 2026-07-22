using K9Crush.Modules.Notifications.Domain;

namespace K9Crush.Modules.Notifications.Api.Commands.UpdateNotificationPreferences;

/// <summary>
/// The request/command for this slice - what the caller sends. Enabled
/// isn't one of the emlang yaml's own named props for this step (the yaml
/// only shows notificationType + mandatory) - see
/// UpdateNotificationPreferencesHandler's doc comment for why it's
/// included anyway (there's no other way for this command to mean
/// anything).
/// </summary>
public sealed record UpdateNotificationPreferencesRequest(NotificationType NotificationType, bool Enabled);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record UpdateNotificationPreferencesResponse(NotificationType NotificationType, bool Enabled);
