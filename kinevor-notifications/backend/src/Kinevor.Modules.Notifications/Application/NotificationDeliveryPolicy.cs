using Kinevor.Modules.Notifications.Domain;

namespace Kinevor.Modules.Notifications.Application;

/// <summary>Outcome of applying preference, device and suppression rules to one notification.</summary>
/// <param name="Allowed">Whether the notification may be handed to a transport.</param>
/// <param name="Reason">Failure reason when <paramref name="Allowed"/> is false.</param>
/// <param name="Now">The clock value used for the decision and resulting timestamp.</param>
public readonly record struct NotificationDeliveryDecision(bool Allowed, string? Reason, DateTime Now);

/// <summary>
/// Shared delivery rules for every adapter. Adapters call <see cref="EvaluateAsync"/> before
/// touching a provider and use <see cref="MarkSent"/> or <see cref="MarkFailed"/> to record the
/// outcome on the notification.
/// </summary>
public sealed class NotificationDeliveryPolicy(
    INotificationPreferenceRepository preferences,
    IDeviceTokenRepository devices,
    IEmailSuppressionRepository suppressions,
    TimeProvider clock)
{
    /// <summary>Applies member preferences, recipient requirements, suppression and device rules.</summary>
    /// <param name="notification">The queued notification being delivered.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<NotificationDeliveryDecision> EvaluateAsync(
        Notification notification,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var now = clock.GetUtcNow().UtcDateTime;
        var preference = await preferences.FindAsync(notification.MemberId, notification.Category, ct);
        if (preference is not null && !preference.Allows(notification.Channel))
        {
            return new NotificationDeliveryDecision(
                false,
                $"Disabled by member preference ({notification.Category}/{ChannelName(notification.Channel)}).",
                now);
        }

        switch (notification.Channel)
        {
            case NotificationChannel.Email:
                if (string.IsNullOrWhiteSpace(notification.RecipientEmail))
                    return new NotificationDeliveryDecision(
                        false,
                        "No recipient e-mail address was provided for the e-mail channel.",
                        now);

                var suppression = await suppressions.FindAsync(notification.RecipientEmail, ct);
                if (suppression is not null)
                    return new NotificationDeliveryDecision(
                        false,
                        $"Recipient e-mail is suppressed ({suppression.Reason}).",
                        now);
                break;

            case NotificationChannel.Push:
                var registered = await devices.ListAsync(notification.MemberId, ct);
                if (registered.Count == 0)
                    return new NotificationDeliveryDecision(
                        false,
                        "No registered push device for the member.",
                        now);
                break;

            default:
                break;
        }

        return new NotificationDeliveryDecision(true, null, now);
    }

    /// <summary>Marks an allowed notification as sent.</summary>
    public NotificationDelivery MarkSent(Notification notification, DateTime now)
    {
        var sent = notification.MarkSent(now);
        return sent.IsFailure
            ? new NotificationDelivery(notification.Id, false, sent.Error.Message)
            : new NotificationDelivery(notification.Id, true, null);
    }

    /// <summary>Marks a notification as failed with a bounded reason.</summary>
    public NotificationDelivery MarkFailed(Notification notification, string reason, DateTime now)
    {
        var bounded = reason.Length <= MaxFailureReasonLength
            ? reason
            : reason[..MaxFailureReasonLength];

        var failed = notification.MarkFailed(bounded, now);
        return failed.IsFailure
            ? new NotificationDelivery(notification.Id, false, failed.Error.Message)
            : new NotificationDelivery(notification.Id, false, bounded);
    }

    private const int MaxFailureReasonLength = 500;

    private static string ChannelName(NotificationChannel channel) => channel switch
    {
        NotificationChannel.Email => "email",
        NotificationChannel.Push => "push",
        _ => "in_app",
    };
}
