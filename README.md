# SafeCare — Adverse Event Reporting System

Author: Justyna Sienkiewicz (justyna.sienkiewicz.stud@pw.edu.pl)

> The application's entire user interface is in Polish, because it is written for a Polish
> hospital. This documentation, the source code, identifiers and log messages are in English.

## 1. Overview

### 1.1. Purpose

SafeCare is a web application for reporting adverse events ("zdarzenia niepożądane") in a
medical facility. It aims to improve communication between patients or their carers and
hospital staff, and to increase patient safety by making medical and organisational errors
visible, traceable and analysable.

From a patient's point of view the priority is that the reporting form is simple, readable and
usable on a mobile device. Reporting is therefore anonymous by default: every personal field is
optional, and a report can be filed without giving any contact details at all.

### 1.2. Technology

| Concern | Choice |
|---|---|
| Framework | .NET 10, Blazor Web App |
| Render mode | Interactive Server (SignalR circuits) |
| UI components | MudBlazor |
| Database | PostgreSQL |
| ORM | Entity Framework Core, code-first |
| Validation | FluentValidation |
| Logging | Serilog (console + rolling file) |
| E-mail | MailKit (SMTP) or Microsoft Graph |

### 1.3. Security

- **Request rate limiting.** Requests from a single IP address are capped at 100 per minute.
  Note the caveat that comes with the chosen technology: under Blazor Server the browser makes
  one HTTP request to load the application and everything afterwards travels over a SignalR
  connection. Sign-in and sign-out are the exceptions, since establishing credentials requires
  a real HTTP request. Form submissions are therefore throttled separately, per circuit, at
  5 submissions per minute.
- **Bot protection on the public form**, in two independent layers:
  - *Honeypot fields* — form fields hidden from human users but readable by bots, deliberately
    made to look like ordinary fields such as an address or a second e-mail.
  - *Minimum completion time* — a genuine user cannot complete the form within a few seconds,
    so a submission that arrives faster than that is treated as a bot.

  A submission caught by either layer fails **silently**: the bot is given no feedback that
  would help it adapt.
- **Open redirect protection.** The `returnUrl` carried through the login flow is accepted only
  when it is a site-relative path.
- **Security headers** (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`,
  `Permissions-Policy`) are added to every response.

### 1.4. Features

The system is split into a public module for reporters and an administrative module for staff.

#### A. Public module (no sign-in)

Intended for patients and their carers; no account is required. The report is filed through a
single sectioned form which collects:

- **Reporter details** — first name, surname, contact details. All optional.
- **Patient details** — first name, surname, date of birth, gender, when the report concerns
  somebody else.
- **Time and place** — either an exact date and time, or a from–to date range when the reporter
  can only place the event within a period, plus the hospital ward chosen from a dictionary.
- **Event type** — categorised (clinical activity, pharmacotherapy, transfusion, equipment and
  organisation). Several events across several categories may be selected, and a free-text
  description may be given instead of, or in addition to, the dictionary entries.
- **Narrative** — a detailed free-text description of what happened.

#### B. Administrative module (sign-in required)

- **Report list** — a sortable, filterable table of all reports.
- **Paging** — for working through large numbers of records.
- **Bookmarkable view state** — filters, sort order and page number are kept in the URL query
  string, so refreshing the page or sending the link to a colleague preserves the current view.
- **Report details** — the full record, with the ability to change its status (New, In progress,
  Resolved, Rejected) or delete it.
- **Account settings** — each staff member chooses whether to receive e-mail notifications
  about new reports.
- **User management** (`Admin` role only) — create staff accounts, assign the `Admin` or `User`
  role, and delete accounts. The system refuses to delete your own account or the last
  remaining administrator.

## 2. Running the project locally

### 2.1. Prerequisites

- .NET 10 SDK
- PostgreSQL reachable on `localhost:5432`
- [MailPit](https://github.com/axllent/mailpit) for e-mail, optional — see below

### 2.2. Local configuration

`SafeCare/appsettings.Development.json` is deliberately excluded from version control and has
to be created locally. It also has to exist for the `dotnet ef` design-time tooling to work:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=SafeCare;Username=postgres;Password=postgres;Include Error Detail=true"
  }
}
```

### 2.3. Running

```bash
dotnet run --project SafeCare
```

The application listens on `http://localhost:5288` and `https://localhost:7163`.

Migrations are applied automatically on startup, and dictionary data plus roughly 200 demo
reports are seeded on first run, so `dotnet ef database update` is normally unnecessary. A
fresh clone needs nothing beyond a reachable PostgreSQL instance.

### 2.4. E-mail in development

Notification e-mail is sent to a local MailPit instance on port 1025. The application runs
perfectly well without it — delivery failures are logged and swallowed by design — but nothing
will be delivered. To start MailPit:

```bash
docker run -d --name mailpit -p 1025:1025 -p 8025:8025 axllent/mailpit
```

The captured messages are then readable at `http://localhost:8025`. A standalone executable is
also available from the MailPit releases page if Docker is not convenient.

Note that a notification is only produced when at least one user has switched the option on
under *Ustawienia konta*; the seeded `admin` account has it off.

### 2.5. Adding a migration

```bash
dotnet ef migrations add <MigrationName> --project SafeCare
```

### 2.6. Tests

There is no test project and no CI pipeline. Verification currently means building the
application and exercising it by hand against a local PostgreSQL instance.

## 3. Credentials

A seeded administrator account is created for testing and evaluation:

- **Username:** `admin`
- **Password:** `Admin123!`

This is a development credential baked into the initial migration. It must be rotated before
any real deployment.

## 4. Screenshots

The captions below are from the original Polish documentation and show the reporter's path and
the administrative panel.

### 4.1. Before signing in — the reporting form

![](./img/Zrzut%20ekranu%202026-02-07%20225641.png)
Fig. 1. Opening screen — reporter and patient details.

![](./img/Zrzut%20ekranu%202026-02-07%20225655.png)
Fig. 2. Choosing the time, place and category of the event.

![](./img/Zrzut%20ekranu%202026-02-07%20231241.png)
Fig. 3. Detailed selection of event types within the chosen categories.

![](./img/Zrzut%20ekranu%202026-02-07%20231351.png)
Fig. 4. Completing the report — the detailed narrative.

### 4.2. Signing in

![](./img/Zrzut%20ekranu%202026-02-07%20225737.png)
Fig. 5. Staff login panel.

### 4.3. After signing in — the administrative panel

![](./img/Zrzut%20ekranu%202026-02-07%20225747.png)
Fig. 6. Dashboard — report table with sorting and filtering.

![](./img/Zrzut%20ekranu%202026-02-07%20225849.png)
Fig. 7. Report details and status editing.

### 4.4. Mobile layout

![](./img/Zrzut%20ekranu%202026-02-07%20232022.png)
Fig. 8. Mobile view of the form.

![](./img/Zrzut%20ekranu%202026-02-07%20232109.png)
Fig. 9. Mobile view of the login panel and the administrative panel.

## 5. Database structure

The schema is relational and managed by Entity Framework Core. It consists of the ASP.NET Core
Identity tables and the domain tables that carry the reporting logic.

### 5.1. Identity tables

- **AspNetUsers** — staff accounts: username, password hash, e-mail, and the two application
  specific columns `FirstName`/`LastName` plus the `ReceiveEmailNotifications` preference.
- **AspNetRoles** — the roles `Admin` and `User`.
- **AspNetUserRoles** and the remaining Identity tables link users to their permissions.

Authorisation is role-based. `Admin` may manage staff accounts; `User` has access to the
reports but not to account management. The role names are defined once, as constants in
`Data/Entities/AppRoles.cs`, and referenced from the `[Authorize]` attributes.

### 5.2. Domain tables and relationships

- **IncidentReports** — the central table holding each report: dates, personal details,
  narrative and status.
  - *One-to-many with Departments* — each report belongs to exactly one ward via the
    `DepartmentId` foreign key; one ward may have many reports.
- **Departments** — dictionary of hospital wards (Ortopedia, Pediatria, …). Normalises the data
  and makes reporting per organisational unit straightforward.
- **IncidentDefinitions** — dictionary of predefined adverse event types, for example
  "niewłaściwa identyfikacja pacjenta" or "podanie niewłaściwej jednostki krwi".
- **IncidentDefinitionIncidentReport** — the join table implementing the many-to-many
  relationship. One report may involve several different event definitions at once (an
  equipment failure *and* human error), and one definition may appear in many reports.

Note that the `Other` category has no rows in `IncidentDefinitions`. It represents the
free-text description a reporter can supply instead of picking from the dictionary, and is
stored directly on the report in the `OtherIncidentDefinition` column.
