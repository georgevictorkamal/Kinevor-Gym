namespace Kinevor.Modules.IdentityAccess.Tests;

/// <summary>Deterministic clock for integration tests that exercise TOTP steps and lockouts.</summary>
public sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    /// <summary>Current UTC instant; assign to advance the test clock.</summary>
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    /// <summary>Returns the configured instant.</summary>
    public override DateTimeOffset GetUtcNow() => UtcNow;
}
