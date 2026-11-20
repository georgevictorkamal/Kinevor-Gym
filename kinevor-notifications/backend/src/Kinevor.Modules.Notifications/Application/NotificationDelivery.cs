using Kinevor.Modules.Notifications.Domain;
using Kinevor.Platform.Application.Common;
using MediatR;

namespace Kinevor.Modules.Notifications.Application;

/// <summary>
/// Delivery seam for external notification channels. The default implementation simulates
/// providers; production hosts can replace it with a real transport such as
/// <see cref="SmtpNotificationDelivery"/>.
/// </summary>
public interface INotificationDeliverySimulator
{
    /// <summary>Attempts delivery for one queued notification.</summary>
    Task<NotificationDelivery> DeliverAsync(Notification notification, CancellationToken ct = default);
}

/// <summary>Default preference-, device- and suppression-aware delivery simulator.</summary>
public sealed class NotificationDeliverySimulator(NotificationDeliveryPolicy policy)
    : INotificationDeliverySimulator
{
    /// <summary>Applies the delivery rules and records sent or failed on the notification.</summary>
    public async Task<NotificationDelivery> DeliverAsync(Notification notification, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var decision = await policy.EvaluateAsync(notification, ct);
        return decision.Allowed
            ? policy.MarkSent(notification, decision.Now)
            : policy.MarkFailed(notification, decision.Reason ?? "Delivery was blocked.", decision.Now);
    }
}

/// <summary>Processes every queued notification that is due, using the simulated providers.</summary>
public record DispatchDueNotificationsCommand : ICommand<DispatchResultDto>;

/// <summary>Handles <see cref="DispatchDueNotificationsCommand"/>.</summary>
public class DispatchDueNotificationsHandler(
    INotificationRepository notifications,
    INotificationDeliverySimulator simulator,
    INotificationsUnitOfWork unitOfWork,
    TimeProvider clock) : IRequestHandler<DispatchDueNotificationsCommand, DispatchResultDto>
{
    /// <summary>Delivers due notifications and persists the resulting statuses.</summary>
    public async Task<DispatchResultDto> Handle(DispatchDueNotificationsCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var due = await notifications.ListDueAsync(clock.GetUtcNow().UtcDateTime, ct);
        var delivered = 0;

        foreach (var notification in due)
        {
            var result = await simulator.DeliverAsync(notification, ct);
            if (result.Delivered) delivered++;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return new DispatchResultDto(due.Count, delivered, due.Count - delivered);
    }
}
