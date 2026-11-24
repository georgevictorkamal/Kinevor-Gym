using Kinevor.Modules.PaymentsBilling.Domain;

namespace Kinevor.Modules.PaymentsBilling.Application;

/// <summary>One capture request handed to a payment gateway.</summary>
/// <param name="MemberId">Member paying.</param>
/// <param name="InvoiceId">Invoice being settled, when any.</param>
/// <param name="Reference">Caller reference; also used as the provider idempotency key.</param>
/// <param name="Amount">Amount to capture.</param>
/// <param name="Currency">ISO 4217 currency.</param>
/// <param name="Method">Payment method.</param>
/// <param name="Description">Human-readable description.</param>
public sealed record PaymentGatewayRequest(
    Guid MemberId,
    Guid? InvoiceId,
    string Reference,
    decimal Amount,
    string Currency,
    PaymentMethod Method,
    string Description);

/// <summary>Outcome of a gateway capture.</summary>
/// <param name="Succeeded">Whether the provider captured the amount.</param>
/// <param name="ProviderReference">Provider transaction id, when returned.</param>
/// <param name="FailureReason">Decline or provider error, when unsuccessful.</param>
public sealed record PaymentGatewayResult(
    bool Succeeded,
    string? ProviderReference = null,
    string? FailureReason = null)
{
    /// <summary>Creates an approved result.</summary>
    public static PaymentGatewayResult Approved(string? providerReference = null) =>
        new(true, providerReference);

    /// <summary>Creates a declined or failed result.</summary>
    public static PaymentGatewayResult Declined(string reason) => new(false, FailureReason: reason);
}

/// <summary>One refund request handed to a payment gateway.</summary>
/// <param name="PaymentId">Payment being refunded.</param>
/// <param name="PaymentReference">Caller reference of the original capture.</param>
/// <param name="ProviderReference">Provider transaction id of the original capture.</param>
/// <param name="RefundReference">Caller reference; also used as the provider idempotency key.</param>
/// <param name="Amount">Amount to return.</param>
/// <param name="Currency">ISO 4217 currency.</param>
/// <param name="Reason">Staff-supplied refund reason.</param>
public sealed record PaymentGatewayRefundRequest(
    Guid PaymentId,
    string PaymentReference,
    string ProviderReference,
    string RefundReference,
    decimal Amount,
    string Currency,
    string Reason);

/// <summary>Outcome of a gateway refund.</summary>
/// <param name="Succeeded">Whether the provider accepted the refund.</param>
/// <param name="ProviderRefundReference">Provider refund id, when returned.</param>
/// <param name="FailureReason">Provider error, when unsuccessful.</param>
public sealed record PaymentGatewayRefundResult(
    bool Succeeded,
    string? ProviderRefundReference = null,
    string? FailureReason = null)
{
    /// <summary>Creates an approved refund result.</summary>
    public static PaymentGatewayRefundResult Approved(string? providerRefundReference = null) =>
        new(true, providerRefundReference);

    /// <summary>Creates a declined or failed refund result.</summary>
    public static PaymentGatewayRefundResult Declined(string reason) =>
        new(false, FailureReason: reason);
}

/// <summary>
/// Small payment gateway port. The module ships a simulated adapter; real providers live in a
/// separate project so the module never references a provider SDK.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Captures a card payment. Non-card methods never reach the gateway.</summary>
    Task<PaymentGatewayResult> CaptureAsync(PaymentGatewayRequest request, CancellationToken ct = default);

    /// <summary>Refunds a captured card payment. Non-card refunds never reach the gateway.</summary>
    Task<PaymentGatewayRefundResult> RefundAsync(
        PaymentGatewayRefundRequest request,
        CancellationToken ct = default);
}

/// <summary>Default adapter that approves every card capture and refund without calling a provider.</summary>
public sealed class SimulatedPaymentGateway : IPaymentGateway
{
    /// <summary>Approves the capture and echoes the caller reference.</summary>
    public Task<PaymentGatewayResult> CaptureAsync(
        PaymentGatewayRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(PaymentGatewayResult.Approved(request.Reference));
    }

    /// <summary>Approves the refund and echoes the caller refund reference.</summary>
    public Task<PaymentGatewayRefundResult> RefundAsync(
        PaymentGatewayRefundRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(PaymentGatewayRefundResult.Approved(request.RefundReference));
    }
}
