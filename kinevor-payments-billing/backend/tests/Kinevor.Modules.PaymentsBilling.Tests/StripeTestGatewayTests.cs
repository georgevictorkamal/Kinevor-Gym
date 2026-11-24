using System.Net;
using System.Text;
using Kinevor.Modules.PaymentsBilling.Application;
using Kinevor.Modules.PaymentsBilling.Domain;
using Kinevor.Modules.PaymentsBilling.StripeGateway;
using Microsoft.Extensions.Options;
using Xunit;

namespace Kinevor.Modules.PaymentsBilling.Tests;

/// <summary>Stripe test-mode adapter contract, exercised without network access.</summary>
public class StripeTestGatewayTests
{
    /// <summary>The adapter posts a confirmed PaymentIntent and returns the provider id.</summary>
    [Fact]
    public async Task CaptureAsync_PostsPaymentIntent()
    {
        var handler = new StubHttpHandler(
            HttpStatusCode.OK,
            """{"id":"pi_test_123","status":"succeeded"}""");
        var gateway = CreateGateway(handler, "sk_test_example");

        var result = await gateway.CaptureAsync(new PaymentGatewayRequest(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "TXN-STRIPE-001",
            12.34m,
            "USD",
            PaymentMethod.Card,
            "Membership"));

        Assert.True(result.Succeeded);
        Assert.Equal("pi_test_123", result.ProviderReference);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/v1/payment_intents", request.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("sk_test_example", request.Headers.Authorization.Parameter);
        Assert.Equal("TXN-STRIPE-001", request.Headers.GetValues("Idempotency-Key").Single());

        var body = Assert.Single(handler.Bodies);
        Assert.Contains("amount=1234", body);
        Assert.Contains("currency=usd", body);
        Assert.Contains("payment_method=pm_card_visa", body);
        Assert.Contains("payment_method_types%5B0%5D=card", body);
        Assert.Contains("confirm=true", body);
        Assert.Contains("metadata%5Breference%5D=TXN-STRIPE-001", body);
        Assert.Contains("metadata%5Binvoice_id%5D=22222222-2222-2222-2222-222222222222", body);
    }

    /// <summary>Provider declines surface as a failed result with the provider message.</summary>
    [Fact]
    public async Task CaptureAsync_ReturnsProviderError()
    {
        var handler = new StubHttpHandler(
            HttpStatusCode.PaymentRequired,
            """{"error":{"message":"Your card was declined."}}""");
        var gateway = CreateGateway(handler, "sk_test_example");

        var result = await gateway.CaptureAsync(Request());

        Assert.False(result.Succeeded);
        Assert.Equal("Your card was declined.", result.FailureReason);
    }

    /// <summary>A live key is refused before any HTTP request leaves the process.</summary>
    [Fact]
    public async Task CaptureAsync_RejectsLiveKey()
    {
        var handler = new StubHttpHandler(
            HttpStatusCode.OK,
            """{"id":"pi_test_123","status":"succeeded"}""");
        var gateway = CreateGateway(handler, "sk_live_example");

        var result = await gateway.CaptureAsync(Request());

        Assert.False(result.Succeeded);
        Assert.Contains("sk_test_", result.FailureReason);
        Assert.Empty(handler.Requests);
    }

    /// <summary>Non-card methods are outside the adapter's scope.</summary>
    [Fact]
    public async Task CaptureAsync_RejectsNonCardMethods()
    {
        var handler = new StubHttpHandler(
            HttpStatusCode.OK,
            """{"id":"pi_test_123","status":"succeeded"}""");
        var gateway = CreateGateway(handler, "sk_test_example");
        var request = Request() with { Method = PaymentMethod.Cash };

        var result = await gateway.CaptureAsync(request);

        Assert.False(result.Succeeded);
        Assert.Empty(handler.Requests);
    }

    /// <summary>The adapter posts a refund against the original PaymentIntent.</summary>
    [Fact]
    public async Task RefundAsync_PostsRefund()
    {
        var handler = new StubHttpHandler(
            HttpStatusCode.OK,
            """{"id":"re_test_123","status":"succeeded"}""");
        var gateway = CreateGateway(handler, "sk_test_example");

        var result = await gateway.RefundAsync(new PaymentGatewayRefundRequest(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "TXN-STRIPE-003",
            "pi_test_123",
            "RF-STRIPE-003",
            4.56m,
            "USD",
            "Member request"));

        Assert.True(result.Succeeded);
        Assert.Equal("re_test_123", result.ProviderRefundReference);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/v1/refunds", request.RequestUri!.AbsolutePath);
        Assert.Equal("RF-STRIPE-003", request.Headers.GetValues("Idempotency-Key").Single());

        var body = Assert.Single(handler.Bodies);
        Assert.Contains("payment_intent=pi_test_123", body);
        Assert.Contains("amount=456", body);
        Assert.Contains("metadata%5Breference%5D=RF-STRIPE-003", body);
        Assert.Contains("metadata%5Breason%5D=Member+request", body);
    }

    /// <summary>A refund without a provider PaymentIntent reference is refused locally.</summary>
    [Fact]
    public async Task RefundAsync_RequiresProviderReference()
    {
        var handler = new StubHttpHandler(
            HttpStatusCode.OK,
            """{"id":"re_test_123","status":"succeeded"}""");
        var gateway = CreateGateway(handler, "sk_test_example");

        var result = await gateway.RefundAsync(new PaymentGatewayRefundRequest(
            Guid.NewGuid(),
            "TXN-STRIPE-004",
            " ",
            "RF-STRIPE-004",
            4.56m,
            "USD",
            "Member request"));

        Assert.False(result.Succeeded);
        Assert.Empty(handler.Requests);
    }

    /// <summary>Provider refund errors surface as a failed result.</summary>
    [Fact]
    public async Task RefundAsync_ReturnsProviderError()
    {
        var handler = new StubHttpHandler(
            HttpStatusCode.BadRequest,
            """{"error":{"message":"Refund exceeds the charge amount."}}""");
        var gateway = CreateGateway(handler, "sk_test_example");

        var result = await gateway.RefundAsync(new PaymentGatewayRefundRequest(
            Guid.NewGuid(),
            "TXN-STRIPE-005",
            "pi_test_123",
            "RF-STRIPE-005",
            4.56m,
            "USD",
            "Member request"));

        Assert.False(result.Succeeded);
        Assert.Equal("Refund exceeds the charge amount.", result.FailureReason);
    }

    private static PaymentGatewayRequest Request() => new(
        Guid.NewGuid(),
        null,
        "TXN-STRIPE-002",
        10m,
        "USD",
        PaymentMethod.Card,
        "Membership");

    private static StripeTestPaymentGateway CreateGateway(HttpMessageHandler handler, string secretKey)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://stripe.test/"),
        };
        return new StripeTestPaymentGateway(
            client,
            Options.Create(new StripeTestGatewayOptions { SecretKey = secretKey }));
    }

    private sealed class StubHttpHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }
}
