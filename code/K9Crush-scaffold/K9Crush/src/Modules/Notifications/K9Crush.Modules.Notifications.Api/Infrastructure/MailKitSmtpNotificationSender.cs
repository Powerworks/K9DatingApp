using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace K9Crush.Modules.Notifications.Api.Infrastructure;

/// <summary>
/// Real SMTP send via MailKit (ADR-027) - in dev/staging, points at
/// smtp4dev (deploy/compose/docker-compose.yml), which captures every
/// send into a local web inbox (http://localhost:5080) rather than
/// actually delivering anything. Production email provider (SendGrid/
/// Postmark/SES) is still an open decision (docs/02-inventory-list.md) -
/// this class only knows how to talk SMTP, not which provider owns
/// production sending; swap the Smtp:Host/Port config (and likely add
/// auth here) once that's decided, no call-site changes needed since
/// callers only see ISmtpNotificationSender.
/// </summary>
public sealed class MailKitSmtpNotificationSender : ISmtpNotificationSender
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _fromAddress;

    public MailKitSmtpNotificationSender(IConfiguration configuration)
    {
        _host = configuration["Smtp:Host"] ?? throw new InvalidOperationException("Missing Smtp:Host");
        _port = int.Parse(configuration["Smtp:Port"] ?? throw new InvalidOperationException("Missing Smtp:Port"));
        _fromAddress = configuration["Smtp:FromAddress"] ?? throw new InvalidOperationException("Missing Smtp:FromAddress");
    }

    public async Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_fromAddress));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        // smtp4dev needs no auth/TLS - SecureSocketOptions.None matches
        // its dev-capture setup.
        await client.ConnectAsync(_host, _port, SecureSocketOptions.None, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
