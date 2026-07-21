using System.ComponentModel.DataAnnotations;

namespace K9Crush.Modules.Notifications.Api.Commands.EditNotificationTemplate;

/// <summary>The request/command for this slice - what the caller sends.</summary>
public sealed record EditNotificationTemplateRequest(
    [property: Required, MaxLength(200)] string Subject,
    [property: Required, MaxLength(2000)] string Body);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record EditNotificationTemplateResponse(Guid TemplateId, bool Locked);
