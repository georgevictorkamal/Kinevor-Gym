using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Kinevor.Modules.PaymentsBilling.Application;
using Kinevor.Modules.PaymentsBilling.Domain;
using Microsoft.Extensions.Options;

namespace Kinevor.Modules.PaymentsBilling.StripeGateway;

/// <summary>
/// Stripe test-mode adapter. It creates and confirms a PaymentIntent with Stripe's test card,
/// uses the caller reference as the idempotency key and never stores card data.
/// </summary>
public sealed class StripeTestPaymentGateway(
    HttpClient http,
    IOptions<StripeTestGatewayOptions> options) : IPaymentGateway
{
    /// <summary>Captures a card payment through Stripe's test-mode API.</summary>
    public async Task<PaymentGatewayResult> CaptureAsync(
        PaymentGatewayRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Method != PaymentMethod.Card)
            return PaymentGatewayResult.Declined("The Stripe test gateway supports card payments only.");

        var settings = options.Value;
        if (settings.SecretKey?.StartsWith("sk_test_", StringComparison.Ordinal) != true)
            return PaymentGatewayResult.Declined("The Stripe adapter requires an sk_test_ secret key.");

        var minorUnits = decimal.Round(request.Amount * 100m, 0, MidpointRounding.AwayFromZero);
        if (minorUnits <= 0m || minorUnits > long.MaxValue)
            return PaymentGatewayResult.Declined("The amount cannot be represented in Stripe minor units.");

        var form = new Dictionary<string, string>
        {
            ["amount"] = ((long)minorUnits).ToString(CultureInfo.InvariantCulture),
            ["currency"] = request.Currency.ToLowerInvariant(),
            ["payment_method"] = settings.TestPaymentMethod,
            ["payment_method_types[0]"] = "card",
            ["confirm"] = "true",
            ["description"] = request.Description,
            ["metadata[reference]"] = request.Reference,
            ["metadata[member_id]"] = request.MemberId.ToString(),
        };
        if (request.InvoiceId is { } invoiceId)
            form["metadata[invoice_id]"] = invoiceId.ToString();

        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/payment_intents")
        {
            Content = new FormUrlEncodedContent(form),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.SecretKey);
        message.Headers.TryAddWithoutValidation("Idempotency-Key", request.Reference);

        using var response = await http.SendAsync(message, ct);
        using var document = await ReadDocumentAsync(response, ct);
        if (document is null)
            return PaymentGatewayResult.Declined(
                $"Stripe returned HTTP {(int)response.StatusCode} with an unreadable body.");

        var root = document.RootElement;
        if (!response.IsSuccessStatusCode)
            return PaymentGatewayResult.Declined(ReadError(root) ?? $"Stripe returned HTTP {(int)response.StatusCode}.");

        var providerReference = ReadString(root, "id");
        var status = ReadString(root, "status");
        return status == "succeeded"
            ? PaymentGatewayResult.Approved(providerReference)
            : PaymentGatewayResult.Declined(
                $"Stripe PaymentIntent status is '{status ?? "unknown"}'; the card was not captured.");
    }

    private static async Task<JsonDocument?> ReadDocumentAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        try
        {
            return JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadError(JsonElement root) =>
        root.TryGetProperty("error", out var error) && error.TryGetProperty("message", out var message)
            ? message.GetString()
            : null;

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) ? value.GetString() : null;
}
