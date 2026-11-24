using System.Net.Mail;
using Kinevor.Modules.Notifications.Application;
using Kinevor.Modules.Notifications.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Kinevor.Modules.Notifications.Tests;

/// <summary>SMTP adapter behavior, suppression rules and captured-message delivery.</summary>
public class SmtpDeliveryTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A permitted e-mail is handed to the transport and recorded as sent.</summary>
    [Fact]
    public async Task DeliverAsync_SendsEmailAndMarksSent()
    {
        var transport = new CapturingEmailTransport();
        var notification = EmailNotification("maya@example.com");
        var delivery = CreateDelivery(transport);

        var result = await delivery.DeliverAsync(notification);

        Assert.True(result.Delivered);
        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.Equal(Now, notification.SentAt);
        var message = Assert.Single(transport.Messages);
        Assert.Equal("maya@example.com", message.To);
        Assert.Equal("Class reminder", message.Subject);
        Assert.Equal("Your class starts soon.", message.Body);
    }

    /// <summary>A suppressed address never reaches the transport and the failure is recorded.</summary>
    [Fact]
    public async Task DeliverAsync_RespectsSuppression()
    {
        var transport = new CapturingEmailTransport();
        var suppression = EmailSuppression.Create("maya@example.com", "bounce").Value;
        var delivery = CreateDelivery(transport, suppression: suppression);
        var notification = EmailNotification("maya@example.com");

        var result = await delivery.DeliverAsync(notification);

        Assert.False(result.Delivered);
        Assert.Empty(transport.Messages);
        Assert.Equal(NotificationStatus.Failed, notification.Status);
        Assert.Contains("suppressed (bounce)", notification.FailureReason);
    }

    /// <summary>A member preference can disable e-mail before the transport is reached.</summary>
    [Fact]
    public async Task DeliverAsync_RespectsPreference()
    {
        var transport = new CapturingEmailTransport();
        var memberId = Guid.NewGuid();
        var preference = NotificationPreference.For(memberId, NotificationCategory.ClassReminders);
        preference.Update(email: false, push: true, inApp: true);
        var delivery = CreateDelivery(transport, preference: preference);
        var notification = EmailNotification("maya@example.com", memberId);

        var result = await delivery.DeliverAsync(notification);

        Assert.False(result.Delivered);
        Assert.Empty(transport.Messages);
        Assert.Contains("Disabled by member preference", notification.FailureReason);
    }

    /// <summary>Transport exceptions become a generic member-facing reason and a detailed log entry.</summary>
    [Fact]
    public async Task DeliverAsync_HidesTransportDetailsFromMembers()
    {
        var failure = new SmtpException("Mailbox unavailable on internal-relay-07.");
        var transport = new CapturingEmailTransport { Failure = failure };
        var logger = new CapturingLogger<SmtpNotificationDelivery>();
        var notification = EmailNotification("maya@example.com");
        var delivery = CreateDelivery(transport, logger: logger);

        var result = await delivery.DeliverAsync(notification);

        Assert.False(result.Delivered);
        Assert.Equal(NotificationStatus.Failed, notification.Status);
        Assert.Equal(SmtpNotificationDelivery.DeliveryFailedReason, notification.FailureReason);
        Assert.DoesNotContain("internal-relay-07", notification.FailureReason);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(failure, entry.Exception);
        Assert.Contains(notification.Id.ToString(), entry.Message);
    }

    /// <summary>The real SMTP transport can capture a message in a pickup directory.</summary>
    [Fact]
    public async Task SmtpEmailTransport_WritesCapturedMessage()
    {
        var pickupDirectory = Path.Combine(
            Path.GetTempPath(),
            $"kinevor-smtp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pickupDirectory);

        try
        {
            var options = Options.Create(new SmtpNotificationOptions
            {
                PickupDirectory = pickupDirectory,
                FromAddress = "gym@kinevor.demo",
                FromName = "Kinevor GYM",
            });
            var transport = new SmtpEmailTransport(options);

            await transport.SendAsync(new EmailMessage(
                "maya@example.com",
                "Receipt for October",
                "Thanks for training with us."));

            var captured = Assert.Single(Directory.GetFiles(pickupDirectory, "*.eml"));
            var content = await File.ReadAllTextAsync(captured);
            Assert.Contains("To: maya@example.com", content, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Subject: Receipt for October", content, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("gym@kinevor.demo", content, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(pickupDirectory, recursive: true);
        }
    }

    /// <summary>SMTP settings reject incomplete configuration before a message is attempted.</summary>
    [Fact]
    public void Options_RejectMissingHostAndSender()
    {
        Assert.False(new SmtpNotificationOptions().IsValid(out var missingHost));
        Assert.Contains("Host", missingHost);

        Assert.False(new SmtpNotificationOptions { Host = "smtp.example.com" }.IsValid(out var missingSender));
        Assert.Contains("FromAddress", missingSender);

        Assert.True(new SmtpNotificationOptions
        {
            Host = "smtp.example.com",
            FromAddress = "gym@example.com",
        }.IsValid(out var error));
        Assert.Null(error);
    }

    /// <summary>SMTP credentials are rejected unless STARTTLS protects them.</summary>
    [Fact]
    public void Options_RejectCleartextCredentials()
    {
        var cleartext = new SmtpNotificationOptions
        {
            Host = "smtp.example.com",
            FromAddress = "gym@example.com",
            UserName = "smtp-user",
            Password = "smtp-password",
            UseStartTls = false,
        };

        Assert.False(cleartext.IsValid(out var error));
        Assert.Contains("UseStartTls", error);

        cleartext.UseStartTls = true;
        Assert.True(cleartext.IsValid(out _));
    }

    /// <summary>SMTP credentials require both halves of the pair.</summary>
    [Fact]
    public void Options_RequireCompleteCredentialPair()
    {
        var incomplete = new SmtpNotificationOptions
        {
            Host = "smtp.example.com",
            FromAddress = "gym@example.com",
            UserName = "smtp-user",
        };

        Assert.False(incomplete.IsValid(out var error));
        Assert.Contains("Password", error);

        incomplete.UserName = null;
        incomplete.Password = "smtp-password";
        Assert.False(incomplete.IsValid(out var passwordOnly));
        Assert.Contains("UserName", passwordOnly);
    }

    private static Notification EmailNotification(
        string recipient,
        Guid? memberId = null) =>
        Notification.Create(
            memberId ?? Guid.NewGuid(),
            "Class reminder",
            "Your class starts soon.",
            NotificationChannel.Email,
            NotificationCategory.ClassReminders,
            recipient).Value;

    private static SmtpNotificationDelivery CreateDelivery(
        IEmailTransport transport,
        NotificationPreference? preference = null,
        EmailSuppression? suppression = null,
        ILogger<SmtpNotificationDelivery>? logger = null)
    {
        var policy = new NotificationDeliveryPolicy(
            new FakePreferenceRepository(preference),
            new FakeDeviceRepository(),
            new FakeSuppressionRepository(suppression),
            new FixedTimeProvider(Now));
        return new SmtpNotificationDelivery(
            policy,
            transport,
            new NotificationDeliverySimulator(policy),
            logger ?? NullLogger<SmtpNotificationDelivery>.Instance);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, Exception? Exception, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, exception, formatter(state, exception)));
    }

    private sealed class CapturingEmailTransport : IEmailTransport
    {
        public List<EmailMessage> Messages { get; } = [];

        public Exception? Failure { get; init; }

        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            if (Failure is not null)
                throw Failure;

            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }

    private sealed class FakePreferenceRepository(NotificationPreference? preference)
        : INotificationPreferenceRepository
    {
        public Task<IReadOnlyList<NotificationPreference>> ListAsync(Guid memberId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NotificationPreference>>(
                preference is null ? [] : [preference]);

        public Task<NotificationPreference?> FindAsync(
            Guid memberId,
            string category,
            CancellationToken ct = default) =>
            Task.FromResult(preference);

        public void Add(NotificationPreference value)
        {
        }
    }

    private sealed class FakeDeviceRepository : IDeviceTokenRepository
    {
        public Task<IReadOnlyList<DeviceToken>> ListAsync(Guid memberId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DeviceToken>>([]);

        public Task<DeviceToken?> FindByTokenAsync(string token, CancellationToken ct = default) =>
            Task.FromResult<DeviceToken?>(null);

        public void Add(DeviceToken token)
        {
        }

        public void Remove(DeviceToken token)
        {
        }
    }

    private sealed class FakeSuppressionRepository(EmailSuppression? suppression)
        : IEmailSuppressionRepository
    {
        public Task<IReadOnlyList<EmailSuppression>> ListAsync(
            string? search,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EmailSuppression>>(
                suppression is null ? [] : [suppression]);

        public Task<EmailSuppression?> FindAsync(string email, CancellationToken ct = default) =>
            Task.FromResult(suppression);

        public void Add(EmailSuppression value)
        {
        }

        public void Remove(EmailSuppression value)
        {
        }
    }
}
