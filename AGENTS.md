# PROJECT KNOWLEDGE BASE

**Generated:** 2026-03-12
**Commit:** 1448c36
**Branch:** main

## OVERVIEW

Medical incident reporting system (Polish: "zdarzenia niepozadane") for hospitals. .NET 10 Blazor Web App (Interactive Server Rendering) with PostgreSQL. Two modules: public multi-step incident form (no auth) and admin dashboard (cookie auth). UI in Polish (pl-PL). MudBlazor component library.

## STRUCTURE

```
SafeCare/                       # Solution root
  SafeCare.slnx                 # Single-project solution
  SafeCare/                     # Main project
    Program.cs                  # Entry: DI, middleware pipeline, auto-migration, seeding
    Components/
      App.razor                 # Root HTML document
      Routes.razor              # Blazor Router + AuthorizeRouteView
      Form/                     # Reusable form components (FormCard, HoneypotFields)
      Layout/                   # MainLayout, ReconnectModal (SignalR)
      Pages/
        Home.razor              # "/" — Public 5-step incident wizard
        Login.razor             # "/login" — Staff authentication
        Dashboard.razor         # "/dashboard" — [Authorize] Incident grid
        IncidentDetails.razor   # "/details/{Id:int}" — [Authorize] Report detail
        AccountSettings.razor   # "/account" — [Authorize] User settings
    Data/
      AppDbContext.cs            # IdentityDbContext<User, IdentityRole<Guid>, Guid>
      DbSeeder.cs               # Embedded SeedData.sql loaded on first run
      Entities/                  # IncidentReport, IncidentDefinition, Department, User
      Migrations/                # 4 EF Core migrations (Code-First)
    Services/                    # Interface + implementation pairs (Scoped DI)
    Email/                       # Dual provider (SMTP/Graph), background queue, retry
    Dtos/                        # Service-layer data transfer objects
    ViewModels/                  # UI-bound models (Vm suffix)
    Mappings/                    # C# 13 extension methods (VM -> DTO)
    Validators/                  # FluentValidation rules
    Enums/                       # Gender, IncidentCategory, ReportStatus
    Exceptions/                  # DomainException, EntityNotFoundException
    Middlewares/                  # SecurityHeadersMiddleware
    Endpoints/                   # POST /signin, POST /signout (Minimal API)
    Utils/                       # Config extension methods (Identity, Email, RateLimiter, Logger)
```

## WHERE TO LOOK

| Task | Location | Notes |
|------|----------|-------|
| Add a page/route | `Components/Pages/` | Use `@page` directive, `[Authorize]` for protected |
| Add business logic | `Services/` | Interface + class pair, register in Program.cs |
| Add DB entity | `Data/Entities/` + `Data/AppDbContext.cs` | Include IEntityTypeConfiguration in entity file |
| Add EF migration | CLI: `dotnet ef migrations add <Name>` | Auto-applied on startup via MigrateAsync() |
| Modify form validation | `Validators/IncidentRegistrationFormValidator.cs` | FluentValidation — Polish error messages |
| Change email behavior | `Email/` | Provider selected via `appsettings.json` Email:Provider |
| Add security headers | `Middlewares/SecurityHeadersMiddleware.cs` | |
| Modify auth | `Utils/IdentityConfig.cs` | Cookie auth, login path: `/login` |
| Modify rate limiting | `Utils/RateLimiter.cs` | 100 req/min per IP (fixed window) |
| Change logging | `appsettings.json` Serilog section | No code changes needed |
| Seed data | `Data/SeedData.sql` | Embedded resource, runs on first startup |
| Map VM to DTO | `Mappings/` | C# 13 `extension` method syntax |

## CONVENTIONS

- **Namespace = directory**: `SafeCare.Services`, `SafeCare.Data.Entities`
- **Service pattern**: Interface + implementation in same file (e.g., `IIncidentReportService` defined inside `IncidentReportService.cs`)
- **DI lifetimes**: Scoped for DB services, Singleton for stateless (BotDetection, EmailQueue), Transient for email clients
- **DbContext access**: Always via `IDbContextFactory<AppDbContext>` (Blazor Server requires factory pattern for concurrent circuits)
- **Entity config**: Fluent API via `IEntityTypeConfiguration<T>` in same file as entity class
- **ViewModels**: `Vm` suffix (`IncidentRegistrationFormVm`, `LoginFormVm`)
- **DTOs**: `Dto` suffix (`IncidentReportDto`, `DepartmentDto`)
- **Enums**: Decorated with `[Display(Name = "Polish label")]` + C# 13 `extension` methods for UI helpers (GetColor, GetIcon, GetDisplayName)
- **Error messages**: Polish language throughout (validation, exceptions, UI)
- **Mappings**: C# 13 `extension(Type)` syntax — NOT AutoMapper
- **Namespace style**: Mixed — both block-scoped `namespace X { }` and file-scoped `namespace X;` exist
- **Primary constructors**: Used on services, middleware, entities, background services

## ANTI-PATTERNS (THIS PROJECT)

- **No `as any`/type suppression equivalent** — Nullable reference types enabled, respect them
- **Email failures are intentionally swallowed** in `IncidentReportService.CreateReport()` — logged but never thrown. This is deliberate for resilience. Do NOT change this pattern.
- **No repository pattern** — Services use `IDbContextFactory` directly. Do NOT introduce a repository layer.
- **No AutoMapper** — Manual mapping via C# 13 extensions in `Mappings/`. Do NOT add AutoMapper.
- **DateTime.Now used** (not UTC) — The app is localized for Polish hospitals. Consistent, intentional.

## UNIQUE STYLES

- **C# 13 `extension` blocks**: Used in `Mappings/` and `Enums/` — this is a bleeding-edge C# feature (extension methods declared via `extension(Type)` syntax)
- **Embedded SQL seeding**: `SeedData.sql` as `<EmbeddedResource>` — loaded via reflection at startup, idempotent
- **Dual email providers**: SMTP (MailKit) for dev, Microsoft Graph (OAuth2) for prod — selected via config string
- **Bot detection**: Honeypot fields + minimum form completion time — validated server-side
- **Query string state**: Dashboard filters/sort/page persisted in URL for bookmarkability
- **BCC-only email delivery**: All notification recipients hidden via BCC; To: field is sender's own address

## COMMANDS

```bash
# Build
dotnet build

# Run (dev)
dotnet run --project SafeCare

# Add EF migration
dotnet ef migrations add <MigrationName> --project SafeCare

# Apply migrations (also runs automatically on startup)
dotnet ef database update --project SafeCare

# Dev URLs
# HTTP:  http://localhost:5288
# HTTPS: https://localhost:7163
```

## NOTES

- **No tests exist** — No test project, no test framework. When adding tests: xUnit recommended, mock DbContextFactory and IEmailQueue
- **No CI/CD** — `.github/workflows/` is empty. No Docker, no Makefile
- **No .editorconfig or analyzers** — No StyleCop, no Roslynator configured
- **PostgreSQL required** — Connection string in `appsettings.json` (dev: `postgres:password@localhost:5432/SafeCare`)
- **Dev email**: Expects MailPit on localhost:1025
- **Test credentials**: admin / Admin123!
- **Typo in codebase**: `IncidentReportFiler.cs` should be `IncidentReportFilter.cs` — known, not fixed
- **`appsettings.Development.json` is gitignored** — Will not be in repo; must be created locally
- **Auto-migration on startup** — `Program.cs` runs `MigrateAsync()` + seed. No manual migration step needed for fresh deploy
- **Blazor ISR + SignalR** — Traditional HTTP rate limiting only applies to initial page load and /signin /signout endpoints. All subsequent UI interaction goes through persistent SignalR connection
