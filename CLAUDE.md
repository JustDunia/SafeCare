# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

SafeCare — a medical adverse-event reporting system ("zdarzenia niepożądane") for Polish hospitals. .NET 10 Blazor Web App using **Interactive Server rendering** (SignalR circuits) + PostgreSQL + MudBlazor. Single project, single solution file (`SafeCare.slnx`).

All user-facing text — UI labels, validation messages, exception messages, snackbars — is in **Polish**. Code, identifiers and log messages are in English.

`AGENTS.md` in the repo root is a companion knowledge base with a file-by-file "where to look" table.

## Commands

```bash
dotnet build
```

```bash
dotnet run --project SafeCare
```

```bash
dotnet ef migrations add <MigrationName> --project SafeCare
```

Dev URLs: `http://localhost:5288`, `https://localhost:7163`. Test login: `admin` / `Admin123!`.

Migrations are applied automatically at startup (`Database.MigrateAsync()` in [Program.cs](SafeCare/Program.cs:56)), so `dotnet ef database update` is rarely needed. Design-time EF tooling reads `appsettings.Development.json` via [AppDbContextFactory.cs](SafeCare/Data/AppDbContextFactory.cs) — that file is gitignored and must exist locally for `dotnet ef` to work.

**There is no test project and no CI.** Verification means building and running the app against a local PostgreSQL (`Host=localhost;Port=5432;Database=SafeCare;Username=postgres;Password=password`) and MailPit on port 1025 for email.

## Architecture

Two modules share one Blazor app:
- **Public** — `/` is an anonymous multi-section incident form ([Home.razor](SafeCare/Components/Pages/Home.razor)).
- **Admin** — `/dashboard`, `/details/{Id}`, `/account` require auth; `/admin/users` requires the `Admin` role.

Request/data flow: `.razor` page → `ViewModels` (`*Vm`) → `Mappings` extension → `Dtos` → `Services` → `IDbContextFactory<AppDbContext>` → EF Core. Validation is FluentValidation classes in `Validators/`, wired to MudBlazor via a `ValidateValue` func exposed on each validator.

### Startup wiring ([Program.cs](SafeCare/Program.cs))

Serilog is configured *before* the host is built and the whole app body sits in a try/catch/`CloseAndFlush`. Registration order matters: rate limiter → DbContext **factory** → Identity → Razor components → MudBlazor → services → email. On boot, in one scope: migrate → `DbSeeder.SeedAsync` → `IdentitySeeder.SeedRolesAndAdminAsync`.

### Auth

Cookie-based ASP.NET Core Identity with `IdentityRole<Guid>` and `Guid` keys. Because Blazor Server can't set cookies from a circuit, login/logout are **Minimal API form posts** to `/signin` and `/signout` ([SigninEndpoints.cs](SafeCare/Endpoints/SigninEndpoints.cs)); [Login.razor](SafeCare/Components/Pages/Login.razor) is a plain `<form method="post" action="/signin">` with `<AntiforgeryToken />`, and errors come back as `?error=` query params. `returnUrl` is validated to relative paths only. Configuration lives in [IdentityConfig.cs](SafeCare/Utils/IdentityConfig.cs).

Roles are the constants in [AppRoles.cs](SafeCare/Data/Entities/AppRoles.cs) (`Admin`, `User`) — always use them, never string literals. Pages guard with `@attribute [Authorize]` or `[Authorize(Roles = AppRoles.Admin)]`; `<NotAuthorized>` in [Routes.razor](SafeCare/Components/Routes.razor) renders `RedirectUnauthorized`, which sends authenticated-but-forbidden users to `/dashboard` and anonymous users to `/login?returnUrl=…`.

`returnUrl` has a sharp edge: `/signin` only accepts paths starting with `/` (that's the open-redirect guard), but `ToBaseRelativePath` returns no leading slash — `RedirectUnauthorized` must prepend one and escape the result, or every returnUrl is silently dropped. Security stamp validation runs every 5 minutes so deleted accounts are signed out promptly; components must still tolerate an authenticated principal with no user row (`MainLayout` degrades instead of throwing, since throwing there kills every page).

The `admin` account is seeded through EF `HasData` in [User.cs](SafeCare/Data/Entities/User.cs) (i.e. inside the initial migration), *not* in `SeedData.sql`. `IdentitySeeder` throws at startup if that account is missing, so don't remove the `HasData` seed.

### Data

`AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>` with `ApplyConfigurationsFromAssembly`. Each entity file also contains its `IEntityTypeConfiguration<T>` class — keep that pairing. Domain entities: `IncidentReport` (M:N `IncidentDefinition`, N:1 `Department`).

Dictionary data and ~200 demo reports come from `Data/SeedData.sql`, an `<EmbeddedResource>` read by reflection and run only when `Departments` is empty.

### Email

Fire-and-forget pipeline: services call `IEmailQueue.Enqueue` (unbounded `Channel`, singleton so it's shared across circuits) → `EmailBackgroundService` drains it and retries 3× with exponential backoff, creating a scope per attempt. Provider is picked from `Email:Provider` config in [EmailConfig.cs](SafeCare/Utils/EmailConfig.cs): `Smtp` → MailKit, `MicrosoftGraph` → Graph/OAuth2. Recipients are always BCC.

### Anti-abuse (public form)

Two independent layers, both applied in `Home.Submit()` before validation:
- `IRateLimitService` — in-memory sliding window, 5 submissions/min keyed by a per-circuit GUID. Distinct from the global ASP.NET `RateLimiter` (100 req/min per IP), which only covers the initial page load and `/signin`, `/signout` — everything else rides the SignalR circuit.
- `IBotDetectionService` + `HoneypotFields` — hidden fields plus a 5-second minimum fill time. On detection the submit **fails silently**; do not add user feedback there.

## Conventions

- Namespace mirrors directory. Both block-scoped and file-scoped namespaces exist in the tree; match the file you're editing.
- Services: interface + implementation in the same file, primary constructors, registered explicitly in `Program.cs`. Scoped for DB-touching services, singleton for stateless ones, transient for email clients.
- **Always** resolve `AppDbContext` through `IDbContextFactory<AppDbContext>` and `using var` — never inject `AppDbContext` into a component or scoped service.
- Mapping uses the C# 13 `extension(Type) { … }` block syntax in `Mappings/` — not AutoMapper.
- Enums carry `[Display(Name = "…")]` plus `extension` helpers (`GetDisplayName`, `GetColor`, `GetIcon`) used directly by the UI.
- Pages call `MainLayout.SetPath(...)` from `OnInitializedAsync` via a `[CascadingParameter] MainLayout` to drive nav highlighting.
- Dashboard filter/sort/page state is round-tripped through the query string; keep new filters bookmarkable.
- `DateTime.Now` (local, not UTC) is used deliberately throughout — the app is single-timezone.

## Deliberate choices — do not "fix"

- No repository layer, no AutoMapper, no unit-of-work abstraction.
- Email enqueue failures inside `IncidentReportService.CreateReport` are caught and logged, never rethrown — a broken mail server must not fail a patient's report.
- Anonymous reporting: every reporter/patient field on the public form is optional by design. Don't make them required.

## Encoding — non-negotiable

All sources are UTF-8; `.editorconfig` pins `charset = utf-8-bom`. Three files were once saved as Windows-1250, which the compiler read as UTF-8: the entire public form rendered as mojibake and the name regex — which spelled the Polish alphabet out as literal characters — rejected names like "Paweł". The regex now uses `\p{L}`, which is ASCII in source form. Prefer Unicode classes over literal character lists, and never save a source file as Windows-1250.

## Sorting and filtering traps

In `IncidentReportService.GetReports`:
- Use `SortDefinition.Descending` as given — it was once negated, inverting every column. The single legitimate flip is `PatientAge`, since the oldest patient has the earliest `PatientDob`.
- Sort by `Department.Name`, not `Department` — ordering by the navigation property sorts by foreign key.
- The `Other` category has no `IncidentDefinitions` rows; a report belongs to it when `OtherIncidentDefinition` is non-empty. Grid chips and the filter must both encode that rule, or filtering by "Inne" silently returns nothing.
- Text filters use `EF.Functions.ILike` with wildcards escaped via `ToContainsPattern`.
