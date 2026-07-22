using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using K9Crush.Modules.Notifications.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Notifications.Api.ReadModels.ViewNotificationPreferences;

/// <summary>
/// State-view slice: the emlang yaml's ManagingNotificationPreferences
/// chapter's "View Notification Preferences" -> "Notification Preferences
/// Viewed", rendering the caller's current preferences (or the all-enabled
/// default, computed in-memory, if they've never saved any - a GET must
/// stay read-only, so unlike UpdateNotificationPreferencesHandler this
/// does NOT persist a document just because none existed yet).
/// </summary>
public static class ViewNotificationPreferencesHandler
{
    [WolverineGet("/api/v1/notifications/preferences")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<NotificationPreferencesResponse> Handle(
        ClaimsPrincipal user,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var ownerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var preference = await session.LoadAsync<NotificationPreference>(ownerId, cancellationToken);

        var entries = Enum.GetValues<NotificationType>()
            .Select(type => new NotificationPreferenceEntry(
                type,
                Enabled: preference?.IsEnabled(type) ?? true,
                Mandatory: false))
            .ToList();

        return new NotificationPreferencesResponse(entries);
    }
}
