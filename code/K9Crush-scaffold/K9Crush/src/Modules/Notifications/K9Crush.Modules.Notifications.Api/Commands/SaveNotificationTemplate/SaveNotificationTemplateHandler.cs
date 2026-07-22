using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Notifications.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Notifications.Api.Commands.SaveNotificationTemplate;

/// <summary>
/// State-change slice: the emlang yaml's "Save Notification Template" ->
/// "Notification Template Saved" - releases the lock EditNotificationTemplateHandler
/// acquired. Only the current lock holder can save; a template that
/// isn't locked at all has nothing in progress to save.
/// </summary>
public static class SaveNotificationTemplateHandler
{
    [WolverinePost("/api/v1/notifications/templates/{templateId:guid}/save")]
    [Authorize(Policy = "Shelter")]
    public static async Task<Results<Ok<SaveNotificationTemplateResponse>, NotFound, ForbidHttpResult, Conflict<string>>> Handle(
        Guid templateId,
        SaveNotificationTemplateRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var template = await session.LoadAsync<NotificationTemplate>(templateId, cancellationToken);
        if (template is null)
            return TypedResults.NotFound();

        if (!template.Locked)
            return TypedResults.Conflict("Nothing to save - no edit in progress.");

        if (template.LockedByOwnerId != callerOwnerId)
            return TypedResults.Forbid();

        template.Save();
        session.Store(template);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new SaveNotificationTemplateResponse(template.Id, request.AppliesToAlreadyQueuedNotifications));
    }
}
