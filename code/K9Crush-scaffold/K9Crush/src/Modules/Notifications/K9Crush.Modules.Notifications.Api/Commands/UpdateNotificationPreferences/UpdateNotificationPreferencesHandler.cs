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
/// stream (defaulted all-enabled) on first update, rather than requiring a
/// separate "initialize preferences" step the yaml doesn't have - under
/// ADR-031 this means StartStream with both the created-default event AND
/// the immediate update event together when no stream exists yet, rather
/// than trying to AppendOne onto a FetchForWriting handle whose Aggregate
/// came back null (that handle has nothing to attach the first event to -
/// verified by this slice's own Layer 3 test, not assumed).
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

        var stream = await session.Events.FetchForWriting<NotificationPreference>(ownerId, cancellationToken);

        if (stream.Aggregate is null)
        {
            var (preference, createdEvent) = NotificationPreference.CreateDefaultNew(ownerId);
            var updatedEvent = preference.SetEnabled(request.NotificationType, request.Enabled);
            session.Events.StartStream<NotificationPreference>(ownerId, createdEvent, updatedEvent);
        }
        else
        {
            var @event = stream.Aggregate.SetEnabled(request.NotificationType, request.Enabled);
            stream.AppendOne(@event);
        }

        await session.SaveChangesAsync(cancellationToken);

        return new UpdateNotificationPreferencesResponse(request.NotificationType, request.Enabled);
    }
}
