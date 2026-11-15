#!/usr/bin/env node
// Creates the labels and "good first issue" / "help wanted" starter issues that make a
// repository welcoming to new contributors.
//
//   node scripts/seed-starter-issues.mjs            # dry run: prints what would be created
//   node scripts/seed-starter-issues.mjs --apply    # creates labels and issues with the GitHub CLI
//
// Requires the GitHub CLI (`gh`) authenticated for the repository when using --apply.
// Re-running is safe for labels (they are upserted); issues are skipped when one with the
// same title already exists.

import { execFileSync } from 'node:child_process';

const REPO = process.env.KINEVOR_REPO ?? 'georgevictorkamal/Kinevor-Gym';
const apply = process.argv.includes('--apply');

const labels = [
  ['good first issue', '7057ff', 'Good for newcomers'],
  ['help wanted', '008672', 'Extra attention is needed'],
  ['enhancement', 'a2eeef', 'New feature or request'],
  ['documentation', '0075ca', 'Improvements or additions to documentation'],
  ['tests', 'fbca04', 'Test coverage'],
  ['security', 'b60205', 'Security hardening'],
  ['area:identity', 'c5def5', 'identity-access module'],
  ['area:payments', 'c5def5', 'payments-billing module'],
  ['area:notifications', 'c5def5', 'notifications module'],
  ['area:trainers', 'c5def5', 'trainers module'],
  ['area:classes-bookings', 'c5def5', 'classes-bookings module'],
  ['area:platform', 'c5def5', 'shared platform kernel'],
  ['area:devops', 'c5def5', 'CI, Docker and releases'],
];

const issues = [
  {
    title: 'Add a French UI language to the platform i18n catalogues',
    labels: ['good first issue', 'enhancement', 'area:platform'],
    body: `## Why
The platform ships English and Arabic catalogues and \`node scripts/verify-repo.mjs\` enforces that they stay in parity. Adding a third language is a self-contained way to learn the i18n setup.

## Where to look
- \`kinevor-platform/web/projects/kinevor-platform-web/src/lib/i18n/data/en.json\`
- \`kinevor-platform/web/projects/kinevor-platform-web/src/lib/i18n/data/ar.json\`
- \`kinevor-platform/web/projects/kinevor-platform-web/src/lib/i18n/data/languages.json\`

## Acceptance criteria
- [ ] \`fr.json\` contains every key present in \`en.json\`
- [ ] The language is registered in \`languages.json\`
- [ ] \`node scripts/verify-repo.mjs\` passes
- [ ] A short note is added to the changelog under "Unreleased"`,
  },
  {
    title: 'Add edge-case tests for booking ownership rules',
    labels: ['good first issue', 'tests', 'area:classes-bookings'],
    body: `## Why
\`BookClassHandler.ResolveMemberId\` and \`CancelBookingHandler.EnsureMayCancel\` decide who may book or cancel on behalf of whom. These are the security-relevant rules of the module and deserve explicit tests.

## Where to look
- \`kinevor-classes-bookings/backend/src/Kinevor.Modules.ClassesBookings/Application/ClassCommands.cs\`
- \`kinevor-classes-bookings/backend/tests/\`

## Cases to cover
- [ ] A member booking with another member's id is rejected (Forbidden)
- [ ] Staff may book on behalf of a member and must supply a member id
- [ ] A member cannot cancel someone else's booking
- [ ] The trainer of a class may cancel its bookings; a different trainer may not
- [ ] A confirmed booking inside the two-hour window cannot be cancelled by a member but can by staff`,
  },
  {
    title: 'Document the booking and waitlist flow with a Mermaid sequence diagram',
    labels: ['good first issue', 'documentation', 'area:classes-bookings'],
    body: `## Why
Waitlist promotion, optimistic-concurrency conflicts and the cancellation window are the most interesting flows in the repository, but they are only visible in code.

## Where to look
- \`kinevor-classes-bookings/backend/src/Kinevor.Modules.ClassesBookings/Application/ClassCommands.cs\`
- \`kinevor-classes-bookings/backend/src/Kinevor.Modules.ClassesBookings/Domain/GymClass.cs\`
- \`kinevor-classes-bookings/backend/src/Kinevor.Modules.ClassesBookings/Domain/ClassBooking.cs\`

## Acceptance criteria
- [ ] A \`sequenceDiagram\` covering book, waitlist, cancel and promote, added to the module README or \`docs/\`
- [ ] The 409 conflict path ("The class changed while booking") is shown`,
  },
  {
    title: 'Replace in-memory trainer search with a database-side query',
    labels: ['good first issue', 'enhancement', 'area:trainers'],
    body: `## Why
Trainer text search currently loads matches and filters in memory (documented in \`kinevor-trainers/docs/architecture.md\`). That is fine for a demo but not for large directories.

## Where to look
- \`kinevor-trainers/backend/src/Kinevor.Modules.Trainers.EntityFrameworkCore/TrainerRepository.cs\`

## Acceptance criteria
- [ ] Search is translated to SQL (for example with \`EF.Functions.Like\`), still case-insensitive
- [ ] Existing trainer tests pass; add one test that proves filtering happens in the query
- [ ] The limitation note in the README and architecture doc is updated`,
  },
  {
    title: 'Refunds should update the invoice status',
    labels: ['help wanted', 'enhancement', 'area:payments'],
    body: `## Why
The module README documents that refunding a payment does not reopen or adjust the invoice, so a fully refunded invoice stays \`Paid\`. A small domain change would make the ledger consistent.

## Where to look
- \`kinevor-payments-billing/backend/src/Kinevor.Modules.PaymentsBilling/Application/PaymentCommands.cs\` (\`RefundPaymentHandler\`)
- \`kinevor-payments-billing/backend/src/Kinevor.Modules.PaymentsBilling/Domain/Invoice.cs\`

## Acceptance criteria
- [ ] A full refund moves the invoice out of \`Paid\`; a partial refund updates the outstanding balance
- [ ] Optimistic concurrency on \`Invoice\` is respected
- [ ] Unit tests cover full, partial and repeated refunds
- [ ] The README "Scope and limitations" entry is updated`,
  },
  {
    title: 'Introduce a payment gateway port with a Stripe test-mode adapter',
    labels: ['help wanted', 'enhancement', 'area:payments'],
    body: `## Why
Payments are intentionally simulated. A gateway port (for example \`IPaymentGateway\`) would let real providers plug in without touching the domain, and a Stripe test-mode adapter would make the module demonstrably real.

## Where to look
- \`kinevor-payments-billing/backend/src/Kinevor.Modules.PaymentsBilling/Application/PaymentCommands.cs\` (\`RecordPaymentCommand\`)

## Acceptance criteria
- [ ] A small port in the application layer; the current behaviour becomes the default "simulated" adapter
- [ ] A separate adapter project or sample; no provider SDK referenced from the module itself
- [ ] Idempotency is preserved (the existing reference-based check)
- [ ] \`node scripts/verify-repo.mjs\` still passes (no cross-module references)`,
  },
  {
    title: 'Add an SMTP delivery adapter for notifications',
    labels: ['help wanted', 'enhancement', 'area:notifications'],
    body: `## Why
Delivery is simulated behind \`INotificationDeliverySimulator\`. An SMTP adapter would be the first real provider and a template for push or SMS.

## Where to look
- \`kinevor-notifications/backend/src/Kinevor.Modules.Notifications/Application/NotificationDelivery.cs\`

## Acceptance criteria
- [ ] A new adapter implementing the seam, configured through options (host, port, credentials via configuration, never committed)
- [ ] Failures are recorded on the notification and respect existing suppression rules
- [ ] A test using a fake SMTP server or a captured message`,
  },
  {
    title: 'Implement TOTP multi-factor authentication',
    labels: ['help wanted', 'enhancement', 'security', 'area:identity'],
    body: `## Why
\`UserAccount.MfaEnabled\` is currently a stored flag only (documented in the module README). A real TOTP flow would turn it into a working feature.

## Where to look
- \`kinevor-identity-access/backend/src/Kinevor.Modules.IdentityAccess/Domain/UserAccount.cs\`
- \`kinevor-identity-access/backend/src/Kinevor.Modules.IdentityAccess/Application/LoginCommand.cs\`

## Acceptance criteria
- [ ] Enrolment endpoint returns a secret and an otpauth URI; confirming a first code enables MFA
- [ ] Login requires a valid code when MFA is enabled; recovery codes are single-use and stored hashed
- [ ] Secrets are encrypted at rest or clearly documented as demo-only
- [ ] Tests cover enrolment, login with and without a code, and recovery-code reuse`,
  },
  {
    title: 'Add account lockout after repeated failed sign-ins',
    labels: ['help wanted', 'enhancement', 'security', 'area:identity'],
    body: `## Why
Rate limiting exists, but there is no per-account lockout. Login is already timing-safe (a dummy hash is verified for unknown accounts), so any lockout must keep responses indistinguishable.

## Where to look
- \`kinevor-identity-access/backend/src/Kinevor.Modules.IdentityAccess/Application/LoginCommand.cs\`

## Acceptance criteria
- [ ] Failed-attempt counter and a temporary lock window on the account
- [ ] Unknown, locked and wrong-password responses remain indistinguishable
- [ ] Tests, including that a successful login resets the counter`,
  },
  {
    title: 'Create an EF Core migrations baseline for one module (pilot)',
    labels: ['help wanted', 'enhancement', 'area:platform'],
    body: `## Why
All ten demo hosts create their SQLite schema with \`EnsureCreatedAsync\`. Production hosts need migrations, and one worked example would show how modules should provide them.

## Where to look
- \`kinevor-members/backend/src/Kinevor.Modules.Members.EntityFrameworkCore/\`
- the module's DemoApi \`Program.cs\`

## Acceptance criteria
- [ ] An initial migration for the members module, kept out of the library API surface
- [ ] The demo host applies migrations instead of \`EnsureCreatedAsync\` for this module
- [ ] Documentation of the pattern so the other nine modules can follow`,
  },
  {
    title: 'Add OpenTelemetry traces and metrics to the platform kernel',
    labels: ['help wanted', 'enhancement', 'area:platform'],
    body: `## Why
The platform already centralises host setup (security headers, rate limiting, demo guard). A shared, opt-in OpenTelemetry extension would give every module traces and metrics for free.

## Acceptance criteria
- [ ] One extension method in the platform package, opt-in and off by default
- [ ] HTTP server and EF Core instrumentation, OTLP exporter configured through standard environment variables
- [ ] Documented in the platform README with a minimal example`,
  },
];

function gh(args, opts = {}) {
  return execFileSync('gh', args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'inherit'], ...opts });
}

console.log(`${apply ? 'APPLY' : 'DRY RUN'} against ${REPO}: ${labels.length} labels, ${issues.length} issues\n`);

if (apply) {
  for (const [name, color, description] of labels) {
    gh(['label', 'create', name, '--repo', REPO, '--color', color, '--description', description, '--force']);
  }
  const existing = new Set(
    JSON.parse(gh(['issue', 'list', '--repo', REPO, '--state', 'all', '--limit', '500', '--json', 'title'])).map((i) => i.title),
  );
  for (const issue of issues) {
    if (existing.has(issue.title)) {
      console.log(`skip (exists): ${issue.title}`);
      continue;
    }
    const args = ['issue', 'create', '--repo', REPO, '--title', issue.title, '--body', issue.body];
    for (const label of issue.labels) args.push('--label', label);
    console.log(gh(args).trim());
  }
} else {
  for (const issue of issues) {
    console.log(`- ${issue.title}\n    labels: ${issue.labels.join(', ')}`);
  }
  console.log('\nRun again with --apply to create them.');
}
