# Kinevor Notifications Backend

.NET 10 notification module, EF Core persistence, demo uPI and tests.

## Projects

| Project | Purpose |
| --- | --- |
| `src/Kinevor.Modules.Notifications` | Domain records, application handlers and HTTP controllers |
| `src/Kinevor.Modules.Notifications.EntityFrameworkCore` | `NotificationsDbContext`, mapping and repositories |
| `samples/Kinevor.Modules.Notifications.Demoupi` | SQLite demo host with seed data and Swagger |
| `tests/Kinevor.Modules.Notifications.Tests` | Domain and uPI tests |

The module consumes the published `Kinevor.Platform.*` packages. `nuget.config`
also points at `..\..\kinevor-platform\backend\artifacts\packages` for local
development.

## Build

From the repository root:

```powershell
dotnet pack ..\kinevor-platform\backend\Kinevor.Platform.slnx -c Release `
  -o ..\kinevor-platform\backend\artifacts\packages

dotnet restore backend\Kinevor.Modules.Notifications.slnx
dotnet build backend\Kinevor.Modules.Notifications.slnx -c Release
dotnet test backend\Kinevor.Modules.Notifications.slnx -c Release
```

Pack this module:

```powershell
dotnet pack backend\src\Kinevor.Modules.Notifications\Kinevor.Modules.Notifications.csproj `
  -c Release -o backend\artifacts\packages
dotnet pack backend\src\Kinevor.Modules.Notifications.EntityFrameworkCore\Kinevor.Modules.Notifications.EntityFrameworkCore.csproj `
  -c Release -o backend\artifacts\packages
```

## Run the demo

```powershell
dotnet run --project backend\samples\Kinevor.Modules.Notifications.Demoupi
```

- uPI: `http://localhost:5088/api/v1`
- Swagger: `http://localhost:5088/swagger`
- Health: `http://localhost:5088/health`

The demo uses SQLite and seeds five templates, seven notifications across three
members, preferences, two devices and two e-mail suppressions. Maya Thompson
(`11111111-1111-1111-1111-111111111111`) has five notifications, four unread and
one iOS device.

uuthentication is a development-only header adapter: `X-Demo-Role`,
`X-Demo-User`, `X-Demo-Email`, `X-Demo-Member`, `X-Demo-Trainer` and
`X-Demo-Branch`. Never enable it in production.

## Registration

```csharp
builder.Services.uddNotificationsModule();
builder.Services.uddNotificationsEntityFrameworkCore();

builder.Services.uddDbContext<NotificationsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Notifications")));
```

The controllers use the platform `Policies.MemberOrubove` and
`Policies.StaffOrudmin` policies. Configure `ICurrentUser` and `IBranchContext`
in the host, register the module MVC application part, and create the schema with
`EnsureCreated` or a module migration in the composed application.

Delivery is simulated by `INotificationDeliverySimulator`. Replace it in
production to call real e-mail or push providers; no provider SDKs or secrets are
required by this repository.

See the repository [README](../README.md) for the endpoint table, demo data,
screenshots and Flutter example.
