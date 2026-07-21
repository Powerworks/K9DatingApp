namespace K9Crush.Modules.Notifications.Api.ReadModels.ViewNotificationTemplates;

public sealed record NotificationTemplateEntry(Guid TemplateId, string Name, bool Locked);

public sealed record NotificationTemplatesResponse(IReadOnlyList<NotificationTemplateEntry> Templates);
