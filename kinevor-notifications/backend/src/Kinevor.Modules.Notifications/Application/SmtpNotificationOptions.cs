namespace Kinevor.Modules.Notifications.Application;

/// <summary>
/// SMTP settings for <see cref="SmtpNotificationDelivery"/>. Bind from the
/// <c>Notifications:Smtp</c> configuration section and supply credentials through user-secrets,
/// environment variables or a secret store. Never commit them.
/// </summary>
public sealed class SmtpNotificationOptions
{
    /// <summary>Configuration section read by the configuration overload.</summary>
    public const string SectionName = "Notifications:Smtp";

    /// <summary>SMTP server host name. Not required when <see cref="PickupDirectory"/> is set.</summary>
    public string? Host { get; set; }

    /// <summary>SMTP server port.</summary>
    public int Port { get; set; } = 587;

    /// <summary>Whether to negotiate TLS with STARTTLS on the network transport.</summary>
    public bool UseStartTls { get; set; } = true;

    /// <summary>Optional SMTP user name. Leave empty for unauthenticated relays.</summary>
    public string? UserName { get; set; }

    /// <summary>Optional SMTP password. Supply it from configuration, never source control.</summary>
    public string? Password { get; set; }

    /// <summary>Envelope sender address.</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Optional display name for the envelope sender.</summary>
    public string? FromName { get; set; } = "Kinevor";

    /// <summary>
    /// Optional directory for captured messages. When set, the transport writes <c>.eml</c> files
    /// instead of opening a network connection, which is useful for local demos and tests.
    /// </summary>
    public string? PickupDirectory { get; set; }

    /// <summary>Network timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Whether the options are complete enough to build a transport.</summary>
    public bool IsValid(out string? error)
    {
        if (string.IsNullOrWhiteSpace(PickupDirectory) && string.IsNullOrWhiteSpace(Host))
            error = $"{SectionName}:Host is required unless PickupDirectory is configured.";
        else if (Port is < 1 or > 65535)
            error = $"{SectionName}:Port must be between 1 and 65535.";
        else if (TimeoutSeconds < 1)
            error = $"{SectionName}:TimeoutSeconds must be positive.";
        else if (string.IsNullOrWhiteSpace(FromAddress))
            error = $"{SectionName}:FromAddress is required.";
        else if (HasCredentials && (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password)))
            error = $"{SectionName}:UserName and Password must be configured together.";
        else if (HasCredentials && !UseStartTls)
            error = $"{SectionName}:UseStartTls must be enabled when credentials are configured.";
        else
            error = null;

        return error is null;
    }

    private bool HasCredentials =>
        !string.IsNullOrWhiteSpace(UserName) || !string.IsNullOrWhiteSpace(Password);
}
