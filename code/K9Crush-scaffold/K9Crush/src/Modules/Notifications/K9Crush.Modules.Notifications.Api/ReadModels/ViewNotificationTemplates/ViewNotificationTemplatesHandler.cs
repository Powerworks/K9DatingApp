using Marten;
using Microsoft.AspNetCore.Authorization;
using K9Crush.Modules.Notifications.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Notifications.Api.ReadModels.ViewNotificationTemplates;

/// <summary>
/// State-view slice: the emlang yaml's ShelterConfiguresNotificationTemplates
/// chapter's "Open Notification Templates" -> "Notification Templates
/// Opened" -> "Notification Templates" view, consolidated into one slice
/// same as elsewhere in this build-out. Gated by Shelter policy - the
/// yaml's persona is Shelter Staff.
///
/// Returns whatever templates actually exist - possibly none, since
/// there's no create/seed endpoint anywhere (see NotificationTemplate.cs).
/// </summary>
public static class ViewNotificationTemplatesHandler
{
    [WolverineGet("/api/v1/notifications/templates")]
    [Authorize(Policy = "Shelter")]
    public static async Task<NotificationTemplatesResponse> Handle(
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var templates = await session.Query<NotificationTemplate>().ToListAsync(cancellationToken);

        var entries = templates
            .Select(t => new NotificationTemplateEntry(t.Id, t.Name, t.Locked))
            .ToList();

        return new NotificationTemplatesResponse(entries);
    }
}
