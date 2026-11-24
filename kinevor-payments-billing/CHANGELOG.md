# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Added an application-level `IPaymentGateway` port with a simulated default and
  a separate non-packable Stripe test-mode sample. The sample calls the REST API
  directly, reuses the payment reference as the idempotency key and refuses live
  keys.
- Added refund and concurrency coverage for full, partial and repeated refunds
  and stale invoice writes.
- Card refunds call `IPaymentGateway.RefundAsync` before the refund is stored.
  Refund references are unique per payment, so a retried refund returns the
  original result instead of calling the provider twice.
- Payments store the provider capture reference so a card refund can target the
  original transaction. The demo uses `EnsureCreatedAsync`, so an existing
  `payments-billing-demo.db` or the Docker `payments-data` volume must be
  recreated once.

### Changed

- Refunds now reduce the linked invoice's paid balance and reopen it as open or
  overdue when it is no longer settled. The demo's recorded refund reopens
  Sofia's invoice.
- Card payments capture through `IPaymentGateway` before the payment is stored;
  cash and bank-transfer payments are unchanged.

## [0.1.0] - 2026-10-20

### Added

- Standalone payments and billing backend with the `Invoice` and `Payment`
  aggregates, refund records, MediatR commands and queries, EF Core persistence,
  SQLite demo host and platform authorization policies.
- Staff dashboard statistics and revenue buckets, searchable invoice and payment
  ledgers, invoice creation, mark-paid, mark-overdue and void workflows.
- Simulated card, cash and bank-transfer payments with reference idempotency,
  invoice ownership checks and outstanding-balance validation.
- Full and partial payment refunds with remaining-refundable checks.
- Member self-service billing summary, invoice list, invoice detail and invoice
  payment endpoints.
- Angular library with dashboard, invoice ledger and invoice detail components,
  typed HTTP gateway, signal facade, providers and routes.
- Angular demo application with the Kinevor visual system, Sora typography and
  Material icons.
- Flutter package with API client, repository, member invoice ledger, invoice
  detail, payment sheet, theme and strings.
- Flutter example application for Windows and web.
- Real web, mobile and Swagger screenshots under `docs/screenshots`.
- NuGet packing, GitHub Actions CI and documented tag-based release steps.
- MIT license and package documentation.
