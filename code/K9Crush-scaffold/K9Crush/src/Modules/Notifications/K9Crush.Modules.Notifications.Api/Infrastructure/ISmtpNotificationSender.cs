namespace K9Crush.Modules.Notifications.Api.Infrastructure;

public interface ISmtpNotificationSender
{
    Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken);
}
