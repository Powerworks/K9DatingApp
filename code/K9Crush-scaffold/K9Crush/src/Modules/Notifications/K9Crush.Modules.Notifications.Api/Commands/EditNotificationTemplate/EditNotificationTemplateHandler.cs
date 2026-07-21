using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Notifications.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Notifications.Api.Commands.EditNotificationTemplate;

/// <summary>
/// State-change slice: the emlang yaml's "Edit Notification Template" ->
/// "Notification Template Edited", except when someone else already has
/// it locked, which the yaml names as its own outcome ("Attempt To Edit
/// Locked Template" -> "Template Edit Blocked") rather than a generic
/// conflict - same "keep the yaml's named outcome visible" pattern as
/// WithdrawApplicationHandler's "Withdrawal Blocked: Already Approved".
/// The caller who already holds the lock can re-submit edits freely
/// (re-editing your own in-progress draft isn't a conflict).
/// </summary>
public static class EditNotificationTemplateHandler
{
    [WolverinePost("/api/v1/notifications/templates/{templateId:guid}/edit")]
    [Authorize(Policy = "Shelter")]
    public static async Task<Results<Ok<EditNotificationTemplateResponse>, NotFound, Conflict<string>>> Handle(
        Guid templateId,
        EditNotificationTemplateRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var template = await session.LoadAsync<NotificationTemplate>(templateId, cancellationToken);
        if (template is null)
            return TypedResults.NotFound();

        if (template.Locked && template.LockedByOwnerId != callerOwnerId)
            return TypedResults.Conflict("Template Edit Blocked.");

        template.Edit(callerOwnerId, request.Subject, request.Body);
        session.Store(template);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new EditNotificationTemplateResponse(template.Id, template.Locked));
    }
}
