using System.Net;
using System.Net.Mail;
using System.Text;
using Kinevor.Modules.Notifications.Domain;
using Microsoft.Extensions.Options;

namespace Kinevor.Modules.Notifications.Application;

/// <summary>One plain-text e-mail handed to an <see cref="IEmailTransport"/>.</summary>
/// <param name="To">Recipient address.</param>
/// <param name="Subject">Message subject.</param>
/// <param name="Body">Plain-text body.</param>
public sealed record EmailMessage(string To, string Subject, string Body);

/// <summary>Transport boundary that makes the SMTP client replaceable in tests.</summary>
public interface IEmailTransport
{
    /// <summary>Sends one plain-text e-mail.</summary>
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

/// <summary>
/// SMTP transport backed by <see cref="SmtpClient"/>. Network credentials come from options; when
/// <see cref="SmtpNotificationOptions.PickupDirectory"/> is set the message is written to disk
/// instead of being sent.
/// </summary>
public sealed class SmtpEmailTransport(IOptions<SmtpNotificationOptions> options) : IEmailTransport
{
    /// <summary>Builds and sends the message.</summary>
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var smtp = options.Value;
        if (!smtp.IsValid(out var error))
            throw new InvalidOperationException(error);

        using var client = new SmtpClient
        {
            Timeout = checked(smtp.TimeoutSeconds * 1000),
        };

        if (!string.IsNullOrWhiteSpace(smtp.PickupDirectory))
        {
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = smtp.PickupDirectory;
        }
        else
        {
            client.Host = smtp.Host!;
            client.Port = smtp.Port;
            client.EnableSsl = smtp.UseStartTls;
            if (!string.IsNullOrWhiteSpace(smtp.UserName))
                client.Credentials = new NetworkCredential(smtp.UserName, smtp.Password);
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(smtp.FromAddress, smtp.FromName ?? string.Empty),
            Subject = message.Subject,
            SubjectEncoding = Encoding.UTF8,
            Body = message.Body,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false,
        };
        mail.To.Add(message.To);

        await client.SendMailAsync(mail, ct);
    }
}

/// <summary>
/// E-mail adapter that applies the shared delivery policy, then sends through SMTP. Other
/// channels continue to use the simulated delivery path so enabling SMTP does not disable push
/// or in-app records.
/// </summary>
public sealed class SmtpNotificationDelivery(
    NotificationDeliveryPolicy policy,
    IEmailTransport transport,
    NotificationDeliverySimulator fallback) : INotificationDeliverySimulator
{
    /// <summary>Delivers the notification and records the outcome.</summary>
    public async Task<NotificationDelivery> DeliverAsync(Notification notification, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (notification.Channel != NotificationChannel.Email)
            return await fallback.DeliverAsync(notification, ct);

        var decision = await policy.EvaluateAsync(notification, ct);
        if (!decision.Allowed)
            return policy.MarkFailed(notification, decision.Reason ?? "Delivery was blocked.", decision.Now);

        try
        {
            await transport.SendAsync(
                new EmailMessage(notification.RecipientEmail!, notification.Title, notification.Body),
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var reason = string.IsNullOrWhiteSpace(exception.Message)
                ? $"SMTP delivery failed ({exception.GetType().Name})."
                : $"SMTP delivery failed: {exception.Message}";
            return policy.MarkFailed(notification, reason, decision.Now);
        }

        return policy.MarkSent(notification, decision.Now);
    }
}
