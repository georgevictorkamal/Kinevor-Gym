using Kinevor.Modules.Notifications.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kinevor.Modules.Notifications.DependencyInjection;

/// <summary>Registers the optional SMTP delivery adapter.</summary>
public static class NotificationsSmtpExtensions
{
    /// <summary>
    /// Replaces the simulated e-mail path with SMTP. Call after
    /// <see cref="NotificationsModule.AddNotificationsModule"/>. Other channels keep the simulated
    /// behavior, and credentials must come from configuration or a secret store.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Host configuration containing the SMTP section.</param>
    public static IServiceCollection AddNotificationsSmtpDelivery(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<SmtpNotificationOptions>()
            .Bind(configuration.GetSection(SmtpNotificationOptions.SectionName))
            .Validate(options => options.IsValid(out _), "SMTP delivery options are incomplete.")
            .ValidateOnStart();

        return services.AddSmtpDelivery();
    }

    /// <summary>Configures the SMTP adapter in code, for tests or custom hosts.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Callback that populates the options.</param>
    public static IServiceCollection AddNotificationsSmtpDelivery(
        this IServiceCollection services,
        Action<SmtpNotificationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<SmtpNotificationOptions>()
            .Configure(configure)
            .Validate(options => options.IsValid(out _), "SMTP delivery options are incomplete.")
            .ValidateOnStart();

        return services.AddSmtpDelivery();
    }

    private static IServiceCollection AddSmtpDelivery(this IServiceCollection services)
    {
        services.TryAddScoped<NotificationDeliveryPolicy>();
        services.TryAddScoped<NotificationDeliverySimulator>();
        services.TryAddScoped<IEmailTransport, SmtpEmailTransport>();
        services.Replace(ServiceDescriptor.Scoped<INotificationDeliverySimulator, SmtpNotificationDelivery>());
        return services;
    }
}
