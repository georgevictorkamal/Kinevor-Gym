using Kinevor.Modules.PaymentsBilling.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Kinevor.Modules.PaymentsBilling.StripeGateway;

/// <summary>Registers the Stripe test-mode gateway in place of the simulated adapter.</summary>
public static class StripeTestGatewayExtensions
{
    /// <summary>
    /// Binds <c>Payments:Stripe</c>, requires an <c>sk_test_</c> secret key and replaces the
    /// default simulated <see cref="IPaymentGateway"/> registration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Host configuration.</param>
    public static IServiceCollection AddStripeTestPaymentGateway(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<StripeTestGatewayOptions>()
            .Bind(configuration.GetSection(StripeTestGatewayOptions.SectionName))
            .Validate(
                options => options.SecretKey?.StartsWith("sk_test_", StringComparison.Ordinal) == true,
                $"{StripeTestGatewayOptions.SectionName}:SecretKey must be a Stripe test-mode key.")
            .Validate(
                options => Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out _),
                $"{StripeTestGatewayOptions.SectionName}:ApiBaseUrl must be an absolute URL.")
            .ValidateOnStart();

        services.AddHttpClient("stripe-test", (provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<StripeTestGatewayOptions>>().Value;
            client.BaseAddress = new Uri(options.ApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.Replace(ServiceDescriptor.Scoped<IPaymentGateway>(provider =>
            new StripeTestPaymentGateway(
                provider.GetRequiredService<IHttpClientFactory>().CreateClient("stripe-test"),
                provider.GetRequiredService<IOptions<StripeTestGatewayOptions>>())));

        return services;
    }
}
