using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Kinevor.Modules.PaymentsBilling.Application;
using Kinevor.Modules.PaymentsBilling.Domain;

namespace Kinevor.Modules.PaymentsBilling.Tests;

/// <summary>Hosts the demo API against a unique temporary SQLite database.</summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"kinevor-payments-{Guid.NewGuid():N}.db");

    /// <summary>Gateway double that records provider calls made by the API.</summary>
    public RecordingPaymentGateway Gateway { get; } = new();

    /// <summary>Points the host at the temporary database and the recording gateway.</summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:PaymentsBilling", $"Data Source={_databasePath}");
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(Gateway);
        });
    }

    /// <summary>Clears pooled connections before deleting the temporary database.</summary>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();

        try
        {
            if (File.Exists(_databasePath))
                File.Delete(_databasePath);
        }
        catch (IOException)
        {
            // A finalizer still holding the file must not fail the test run.
        }
    }
}

/// <summary>Approving gateway that keeps the capture and refund requests for assertions.</summary>
public sealed class RecordingPaymentGateway : IPaymentGateway
{
    /// <summary>Capture requests received by the API host.</summary>
    public List<PaymentGatewayRequest> Captures { get; } = [];

    /// <summary>Refund requests received by the API host.</summary>
    public List<PaymentGatewayRefundRequest> Refunds { get; } = [];

    /// <summary>Approves a capture and returns a deterministic provider reference.</summary>
    public Task<PaymentGatewayResult> CaptureAsync(
        PaymentGatewayRequest request,
        CancellationToken ct = default)
    {
        Captures.Add(request);
        return Task.FromResult(PaymentGatewayResult.Approved($"pi_{request.Reference.ToLowerInvariant()}"));
    }

    /// <summary>Approves a refund and returns a deterministic provider reference.</summary>
    public Task<PaymentGatewayRefundResult> RefundAsync(
        PaymentGatewayRefundRequest request,
        CancellationToken ct = default)
    {
        Refunds.Add(request);
        return Task.FromResult(PaymentGatewayRefundResult.Approved($"re_{request.RefundReference.ToLowerInvariant()}"));
    }
}
