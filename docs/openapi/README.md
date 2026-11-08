# OpenAPI documents

Committed snapshots of every demo API's Swagger document. They make the HTTP
surface reviewable without starting ten processes and give client generators a
stable input.

| Service | Document | Paths |
| --- | --- | ---: |
| Identity & access | [`identity-access.json`](identity-access.json) | 6 |
| Members | [`members.json`](members.json) | 12 |
| Memberships | [`memberships.json`](memberships.json) | 17 |
| Classes & bookings | [`classes-bookings.json`](classes-bookings.json) | 10 |
| Check-ins & access | [`checkins-access.json`](checkins-access.json) | 12 |
| Payments & billing | [`payments-billing.json`](payments-billing.json) | 17 |
| Notifications | [`notifications.json`](notifications.json) | 18 |
| Trainers | [`trainers.json`](trainers.json) | 3 |
| Staff dashboard | [`staff-dashboard.json`](staff-dashboard.json) | 4 |
| Public site | [`public-site.json`](public-site.json) | 12 |

The machine-readable index is [`index.json`](index.json).

## Regenerate

```bash
npm run openapi:export
```

The script packs the platform packages, builds every `DemoApi`, starts each host
against a throwaway working directory, downloads `/swagger/v1/swagger.json`,
formats it and stops the host. It writes `index.json` last.

Run it whenever a controller, route, request DTO or response DTO changes. The
documents describe the standalone demo API, including `/health`; the demo
`X-Demo-Role` security scheme is part of the generated spec.

Interactive Swagger UI is still available at `/swagger` while a demo host runs.
See [the deployment guide](../deployment.md) for ports and startup commands.
