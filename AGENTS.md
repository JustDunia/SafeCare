# PROJECT KNOWLEDGE BASE

## OVERVIEW

Adverse-event reporting system ("zdarzenia niepożądane") for Polish hospitals. .NET 10 Blazor
Web App using Interactive Server rendering with PostgreSQL. Two modules: a public, anonymous
incident form and an admin dashboard behind cookie authentication. UI text is Polish; code,
identifiers, comments and log messages are English. MudBlazor component library.

Single project, single solution file (`SafeCare.slnx`).

## STRUCTURE

```
SafeCare/                          # Solution root
  SafeCare.slnx                    # Single-project solution
  .editorconfig                    # Pins charset=utf-8-bom — see ENCODING below
  SafeCare/                        # Main project
    Program.cs                     # Entry: DI, middleware pipeline, auto-migration, seeding
    Components/
      App.razor                    # Root HTML document (lang="pl")
      Routes.razor                 # Router + AuthorizeRouteView
      ConfirmDialog.razor          # Generic "are you sure?" dialog (report deletion)
      DeleteUserDialog.razor       # User-deletion confirmation dialog
      RedirectUnauthorized.razor   # <NotAuthorized> handler; see AUTH below
      Form/                        # FormCard, FormHeader, HoneypotFields
      Layout/                      # MainLayout, ReconnectModal (SignalR)
      Pages/
        Home.razor                 # "/"                    Public sectioned incident form
        Login.razor                # "/login"                Staff authentication
        Dashboard.razor            # "/dashboard"            [Authorize] Incident grid
        IncidentDetails.razor      # "/details/{Id:int}"     [Authorize] Report detail
        AccountSettings.razor      # "/account"              [Authorize] Notification opt-in
        Admin/Users.razor          # "/admin/users"          [Authorize(Roles = Admin)]
        Error.razor, NotFound.razor
    Data/
      AppDbContext.cs              # IdentityDbContext<User, IdentityRole<Guid>, Guid>
      AppDbContextFactory.cs       # Design-time factory for `dotnet ef`
      DbSeeder.cs                  # Runs embedded SeedData.sql on first run
      IdentitySeeder.cs            # Creates roles, ensures admin holds the Admin role
      Entities/                    # IncidentReport, IncidentDefinition, Department, User, AppRoles
      Migrations/                  # 4 EF Core migrations (code-first)
      SeedData.sql                 # <EmbeddedResource>, dictionaries + ~200 demo reports
    Services/                      # Interface + implementation pairs
    Email/                         # Dual provider (SMTP/Graph), background queue, retry
    Dtos/                          # Service-layer data transfer objects
    ViewModels/                    # UI-bound models (Vm suffix)
    Mappings/                      # C# 13 extension blocks (VM <-> DTO)
    Validators/                    # FluentValidation rules
    Enums/                         # Gender, IncidentCategory, ReportStatus
    Exceptions/                    # DomainException, EntityNotFoundException
    Middlewares/                   # SecurityHeadersMiddleware
    Endpoints/                     # POST /signin, POST /signout (Minimal API)
    Utils/                         # Config extensions (Identity, Email, RateLimiter, Logger)
```

## WHERE TO LOOK

| Task | Location | Notes |
|------|----------|-------|
| Add a page/route | `Components/Pages/` | `@page` directive; `[Authorize]` for protected pages |
| Add business logic | `Services/` | Interface + class pair, register in `Program.cs` |
| Add DB entity | `Data/Entities/` | Include the `IEntityTypeConfiguration` in the same file |
| Add EF migration | `dotnet ef migrations add <Name> --project SafeCare` | Auto-applied on startup |
| Change public form validation | `Validators/IncidentRegistrationFormValidator.cs` | Polish messages |
| Change user-creation validation | `Validators/AdminUserCreateValidator.cs` | Mirrored server-side in `UserManagementService` |
| Change dashboard filtering/sorting | `Services/IncidentReportService.GetReports` | See SORTING AND FILTERING below |
| Change email behaviour | `Email/` | Provider from `Email:Provider` config |
| Add security headers | `Middlewares/SecurityHeadersMiddleware.cs` | |
| Modify auth | `Utils/IdentityConfig.cs` | Cookie auth, login path `/login` |
| Modify roles | `Data/Entities/AppRoles.cs` | Constants only — never string literals |
| Modify rate limiting | `Utils/RateLimiter.cs`, `Services/RateLimitService.cs` | Two separate layers |
| Change logging | `appsettings.json` Serilog section | No code change needed |
| Seed data | `Data/SeedData.sql` | Embedded, runs only when `Departments` is empty |
| Map VM to DTO | `Mappings/` | C# 13 `extension` syntax |

## ENCODING

**All source files are UTF-8 and `.editorconfig` pins `charset = utf-8-bom`. Do not change
this, and do not save a source file in Windows-1250.**

This is not a style preference. Three files (`Home.razor`, `HoneypotFields.razor`,
`IncidentRegistrationFormValidator.cs`) were once saved as Windows-1250. The C# compiler reads
sources as UTF-8, so every Polish diacritic decoded to a replacement character. The whole
public form rendered as mojibake, and — because the name-validation regex spelled out the
Polish alphabet as literal characters — the form rejected ordinary Polish names such as
"Paweł".

The regex now uses the Unicode letter class `\p{L}`, which is pure ASCII in source form and
therefore cannot break this way again. Prefer that approach over literal character lists.

## AUTH

Cookie-based ASP.NET Core Identity with `IdentityRole<Guid>` and `Guid` keys.

Blazor Server cannot set cookies from inside a circuit — by the time a SignalR message arrives
the HTTP response is long gone. Sign-in and sign-out are therefore **Minimal API form posts**
to `/signin` and `/signout` (`Endpoints/SigninEndpoints.cs`). `Login.razor` is a plain
`<form method="post" action="/signin">` carrying an `<AntiforgeryToken />`, and errors come
back as `?error=` query parameters.

`returnUrl` handling has a sharp edge worth remembering: `/signin` only accepts site-relative
paths starting with `/`, which is what prevents an open redirect. `NavigationManager.ToBaseRelativePath`
returns a path *without* a leading slash, so `RedirectUnauthorized` has to prepend one and
escape the result — otherwise every returnUrl is silently discarded and users always land on
`/dashboard`.

Pages guard with `@attribute [Authorize]` or `[Authorize(Roles = AppRoles.Admin)]`.
`<NotAuthorized>` in `Routes.razor` renders `RedirectUnauthorized`, which sends
authenticated-but-forbidden users to `/dashboard` and anonymous users to `/login?returnUrl=…`.

The `admin` account is seeded through EF `HasData` in `User.cs` — i.e. inside the initial
migration — *not* in `SeedData.sql`. `IdentitySeeder` throws at startup if that account is
missing, so the `HasData` seed must not be removed.

Security stamp validation runs every 5 minutes (not the 30-minute default), so a user whose
account an admin has just deleted is signed out reasonably promptly. Components must still
tolerate an authenticated principal with no matching user row — `MainLayout` degrades to a
shell without the account menu rather than throwing, since throwing there takes down every page.

## SORTING AND FILTERING

`IncidentReportService.GetReports` backs the dashboard grid. Two details are easy to get wrong:

- **Sort direction is used as given.** `SortDefinition.Descending` was once negated here, which
  inverted every column and made the default view show the oldest reports first. The one place
  the direction legitimately flips is `PatientAge`, because age is derived from `PatientDob`:
  the oldest patient has the *earliest* date of birth.
- **Sort by `Department.Name`, not `Department`.** Ordering by the navigation property makes EF
  order by the foreign key, so the column sorts by ward ID instead of alphabetically.
- **The `Other` category has no dictionary rows.** A report belongs to it when
  `OtherIncidentDefinition` is non-empty. The grid appends the chip in memory after the page is
  materialised, and the filter has to reproduce that rule against `OtherIncidentDefinition` —
  matching only through the `IncidentDefinitions` join makes filtering by "Inne" return nothing.

Text filters use `EF.Functions.ILike` with wildcards escaped by `ToContainsPattern`, rather than
`.ToLower().Contains(...)`, which cannot use an index and applies .NET casing rules to data the
database collates itself.

## CONVENTIONS

- **Namespace = directory**: `SafeCare.Services`, `SafeCare.Data.Entities`.
- **Service pattern**: interface + implementation in the same file.
- **DI lifetimes**: scoped for DB-touching services, singleton for stateless ones and for
  `IEmailQueue` (the channel must be shared across circuits), transient for email clients.
- **DbContext access**: always via `IDbContextFactory<AppDbContext>` and `using var`. Never
  inject `AppDbContext` into a component or scoped service.
- **Entity config**: fluent API via `IEntityTypeConfiguration<T>` in the same file as the entity.
- **ViewModels** carry the `Vm` suffix; **DTOs** carry `Dto`.
- **Enums** are decorated with `[Display(Name = "Polish label")]` plus C# 13 `extension` helpers
  (`GetDisplayName`, `GetColor`, `GetIcon`) used directly by the UI.
- **Mappings** use the C# 13 `extension(Type) { … }` syntax — not AutoMapper.
- **User-facing text is Polish**; code, comments and logs are English.
- **Pages call `MainLayout.SetPath(...)`** from `OnInitializedAsync` via a
  `[CascadingParameter] MainLayout` to drive nav highlighting.
- **Dashboard state round-trips through the query string** — keep new filters bookmarkable.
- **Namespace style is mixed** (block-scoped and file-scoped both occur). Match the file you are
  editing; `.editorconfig` suggests file-scoped for new code.

## DELIBERATE CHOICES — DO NOT "FIX"

- **No repository layer, no AutoMapper, no unit-of-work abstraction.** Services use
  `IDbContextFactory` directly.
- **Email enqueue failures inside `IncidentReportService.CreateReport` are caught and logged,
  never rethrown.** A broken mail server must not fail a patient's report.
- **Bot detection fails silently.** No user feedback on the honeypot path — feedback would only
  help a bot adapt.
- **`DateTime.Now` (local, not UTC) is used throughout.** The application is single-timezone.
- **Anonymous reporting.** Every reporter and patient field on the public form is optional by
  design; do not make them required.

## NOTES

- **No tests, no CI.** No test project and no `.github/workflows`. When adding tests, xUnit is
  the natural choice; mock `IDbContextFactory` and `IEmailQueue`.
- **PostgreSQL required** — dev connection string in `appsettings.json`.
- **`appsettings.Development.json` is gitignored** and must be created locally; the design-time
  EF tooling reads it through `AppDbContextFactory`.
- **Dev email** expects MailPit on `localhost:1025` (web UI on 8025). The app runs fine without
  it; delivery failures are logged and swallowed.
- **Test credentials**: `admin` / `Admin123!` — a development credential baked into the initial
  migration. Rotate before any real deployment.
- **Auto-migration on startup** — `Program.cs` runs `MigrateAsync()` and then seeds. A fresh
  clone needs nothing but a reachable database.
- **Blazor Server + SignalR** — HTTP rate limiting only covers the initial page load and
  `/signin` / `/signout`. All other interaction rides the circuit, which is why
  `IRateLimitService` exists as a separate per-circuit layer.
- **`Microsoft.Kiota.Abstractions` is referenced directly** to override the vulnerable version
  that `Microsoft.Graph` 5.x pulls in transitively, avoiding the Graph 5 → 6 major upgrade.
