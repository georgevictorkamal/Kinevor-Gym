# Kinevor.Modules.PaymentsBilling

Domain and application layer for Kinevor invoices, payments, refunds and revenue
reporting. The package contains the `Invoice` and `Payment` aggregates, MediatR
commands and queries, DTOs, the `IPaymentGateway` port and the staff/member API
controllers.

## Install

```powershell
dotnet add package Kinevor.Modules.PaymentsBilling
```

The package can be published to GitHub Packages. Configure the
`georgevictorkamal` source and authenticate with a token that can read
packages before restoring.

## Register

```csharp
using System.Text.Json.Serialization;
using Kinevor.Modules.PaymentsBilling.Api;
using Kinevor.Modules.PaymentsBilling.DependencyInjection;

builder.Services.AddPaymentsBillingModule();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .AddApplicationPart(typeof(InvoicesController).Assembly);
```

`AddPaymentsBillingModule` registers the MediatR handlers, `TimeProvider` and the
simulated `IPaymentGateway` adapter. Replace the gateway registration to capture
card payments through a provider; the repository ships a non-packable Stripe
test-mode sample. The host must also register `ICurrentUser`, `IBranchContext`,
authorization policies, authentication and the repository implementations. The
companion `Kinevor.Modules.PaymentsBilling.EntityFrameworkCore` package supplies
EF Core repositories and the unit of work.

## API surface

- Staff: `/api/v1/billing/stats`, `/revenue`, `/invoices` and `/payments`.
- Member: `/api/v1/me/invoices`, `/me/invoices/summary`,
  `/me/invoices/{id}/pay` and `/me/payments`.
- Policies: `StaffOrAdmin` for staff routes and `MemberOrAbove` for member
  routes.
- Responses use the `Kinevor.Platform` `ApiResponse<T>` envelope; enums are
  serialized as strings.

## Behaviour

- Invoice totals are derived from lines, including quantity, unit price and tax
  rate. Currency codes are validated against ISO 4217 minor units.
- Payments support `Card`, `Cash` and `BankTransfer`, reference-level
  idempotency, ownership checks and outstanding-balance validation. Card
  captures go through `IPaymentGateway`; the default adapter is simulated.
- Refunds are recorded against the captured payment, enforce the remaining
  refundable amount and reduce the linked invoice's paid balance. A refunded
  invoice reopens as open or overdue when it is no longer settled.
- Invoice lifecycle transitions cover mark-paid, mark-overdue and void.
- Member queries bind the authenticated member id into every lookup.

The demo API, seed data and Swagger explorer live in the repository's
`backend/samples/Kinevor.Modules.PaymentsBilling.DemoApi` project.

## Licence

MIT.
