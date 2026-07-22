using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using K9Crush.Modules.Notifications.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Notifications.Api.Commands.UpdateNotificationPreferences;

/// <summary>
/// State-change slice: the emlang yaml's ManagingNotificationPreferences
/// chapter's "Update Notification Preferences" -> "Notification
/// Preferences Updated". Lazy-creates the caller's NotificationPreference
/// document (defaulted all-enabled) on first update, rather than
/// requiring a separate "initialize preferences" step the yaml doesn't
/// have.
///
/// The yaml's command/event both carry a "mandatory" prop alongside
/// notificationType, with no scenario exercising a true case. Read two
/// ways: (a) a per-type system flag ("this category can't be disabled")
/// or (b) caller-supplied data. (a) doesn't belong on a member
/// self-service request at all (mandatory-ness would be platform policy,
/// not something a member sets on themselves); (b) has no test showing
/// what a member setting mandatory=true would even mean. Not modeled as
/// stored/settable here - Enabled (not in the yaml's own props, but the
/// only way this command can mean anything - see UpdateNotificationPreferencesRequest)
/// is the actual toggle.
/// </summary>
public static class UpdateNotificationPreferencesHandler
{
    [WolverinePost("/api/v1/notifications/preferences")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<UpdateNotificationPreferencesResponse> Handle(
        UpdateNotificationPreferencesRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var ownerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var preference = await session.LoadAsync<NotificationPreference>(ownerId, cancellationToken)
            ?? NotificationPreference.CreateDefault(ownerId);

        preference.SetEnabled(request.NotificationType, request.Enabled);
        session.Store(preference);
        await session.SaveChangesAsync(cancellationToken);

        return new UpdateNotificationPreferencesResponse(request.NotificationType, request.Enabled);
    }
}
