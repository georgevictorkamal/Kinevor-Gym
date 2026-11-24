using Kinevor.Modules.IdentityAccess.Application;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kinevor.Modules.IdentityAccess.DependencyInjection;

/// <summary>Registers the identity and access application services.</summary>
public static class IdentityAccessModule
{
    /// <summary>Adds MediatR handlers, the password hasher, token service, clock and MFA secret protector.</summary>
    public static IServiceCollection AddIdentityAccessModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<LoginHandler>());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IAuthTokenService, JwtAuthTokenService>();
        services.AddSingleton<IMfaService, TotpMfaService>();
        services.AddDataProtection();
        services.TryAddSingleton<IMfaSecretProtector, DataProtectionMfaSecretProtector>();
        return services;
    }
}
