namespace Kinevor.Modules.PaymentsBilling.StripeGateway;

/// <summary>
/// Stripe test-mode settings. Register the adapter only with an <c>sk_test_</c> key; the adapter
/// refuses live keys so a demo checkout cannot move real money.
/// </summary>
public sealed class StripeTestGatewayOptions
{
    /// <summary>Configuration section read by the registration helper.</summary>
    public const string SectionName = "Payments:Stripe";

    /// <summary>Stripe test-mode secret key. Supply it from user-secrets or the environment.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Stripe API base address, overridable for a local stub.</summary>
    public string ApiBaseUrl { get; set; } = "https://api.stripe.com/";

    /// <summary>Stripe test payment method used for server-side confirmation.</summary>
    public string TestPaymentMethod { get; set; } = "pm_card_visa";
}
