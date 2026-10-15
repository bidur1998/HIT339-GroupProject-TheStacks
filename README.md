# The Stacks: Multi-Branch Library Management System

HIT339 Distributed Development, Group Project: *Project Management and Horizontal Feature Expansion*.

This repository takes my Assignment 2 Library Management System (ASP.NET Core MVC, EF Core, SQLite, ASP.NET Core Identity) as the baseline and expands it outwards with new channels, cross-cutting modules and integrations. It targets **.NET 10**.

> Working arrangement: I (Bidur Chautariya, s374277) am completing this group project on my own. No other team members contributed to this repository. Use of AI assistance is acknowledged in the project portfolio, as required by the unit's rules.

---

## What's new compared with Assignment 2

| # | Brief requirement | What was built | Where to find it |
|---|---|---|---|
| 1 | Public self-service kiosk | Touch-friendly kiosk with its own layout: scan library card → PIN → check out items / account summary / cancel holds. Auto sign-out when idle. | `/Kiosk` · `KioskController` · `Views/Kiosk` · `wwwroot/css/kiosk.css`, `wwwroot/js/kiosk.js` |
| 2 | Public API & developer endpoint | Secure **read-only** REST API (GET only, API key, rate limited, CORS, DTOs only) for catalogue items, categories and branch operating status. | `/api/v1/...` · `Api/CatalogueApiController.cs` · `/Home/Developers` · `LibraryManagementSystem.http` |
| 3 | Automated multi-channel notifications | Simulated email and SMS when items are borrowed, due soon, overdue (fine accruing), fined, fine paid, and for reservations. Background job runs hourly; staff can also run it on demand. Everything is logged to a dashboard table and the console. | `/Notifications` · `Services/NotificationService.cs`, `ReminderService.cs` |
| 4 | Multi-branch inventory | Branches (hours, temporary closure), each item has a **home branch** and **current location**, desks belong to a branch, inter-branch **transfers** (Requested → In transit → Received). | `/Branches`, `/Transfers`, `/Desk` · `Services/TransferService.cs` |
| 5 | Reservations & waitlist | Patrons place holds (card + PIN) on Borrowed/Damaged items. First come, first served. When the item becomes Available, the next patron is notified, or a transfer is created automatically if they want to collect it at another branch. Uncollected holds expire. | `/Reservations/Place/{id}`, `/Reservations` · `Services/ReservationService.cs` |
| 6 | External book/media data importer | Admin CSV bulk import with a row-by-row validation preview, plus lookup in a **mock** external metadata provider. | `/Import` · `Services/CatalogueImportService.cs`, `CsvParser.cs`, `MediaMetadataProvider.cs` |
| 7 | Expanded manager analytics & export | Borrowing statistics, fine revenue audit, inventory health. Filter by branch and date, and download as **CSV** or **PDF**. | `/Manager/Reports` · `Services/Reports/*` |

I also fixed some gaps I'd noted in my Assignment 2 report:
- Reception can now **record a fine as paid**. Before this, a fined borrower could never borrow again.
- Borrower edit no longer resets the registration date.
- Deleting an item or borrower with history shows a clear message instead of a database error.
- An admin can't set an item to "Borrowed" without a loan.

---

## 1. Requirements

- **.NET 10 SDK**. Check with `dotnet --list-sdks`. Visual Studio 2026 includes it; otherwise download it from https://dotnet.microsoft.com/download/dotnet/10.0
- **Git**
- No database server is needed. The app uses SQLite, which is a single file (`library.db`) created automatically.
- **Put the project in a short folder path outside OneDrive**, e.g. `C:\Projects\TheStacks`. The EF Core design-time package copies files with very long names into `bin`, so a deep folder (or one OneDrive is syncing) fails the build with *"Could not find a part of the path ... BuildHost-netcore ..."*. If that happens, move the folder, delete the `bin` and `obj` folders, and build again.

## 2. Install and run (the same steps at every branch location)

```bash
# 1. Download the code
git clone <REPOSITORY-URL>
cd <repository-folder>

# 2. Restore NuGet packages and build
dotnet restore
dotnet build

# 3. Run it for YOUR branch location: pick one of DAR, CAS or PAL
dotnet run --project LibraryManagementSystem --launch-profile Branch-DAR
```

Then open the URL printed in the console (e.g. `https://localhost:7050`). If the browser warns about the development certificate, run `dotnet dev-certs https --trust` once, or use the `http://` URL.

**In Visual Studio:** open `LibraryManagementSystem.slnx`, choose **Branch-DAR**, **Branch-CAS** or **Branch-PAL** from the green Run button's drop-down, and press Run.

### Choosing the branch location

Every location uses the same code and the same steps. Only the branch code changes:

| Branch | Code | Launch profile | Port (https / http) |
|---|---|---|---|
| Darwin City | `DAR` | `Branch-DAR` | 7050 / 5050 |
| Casuarina | `CAS` | `Branch-CAS` | 7051 / 5051 |
| Palmerston | `PAL` | `Branch-PAL` | 7052 / 5052 |

The launch profile sets the setting `BranchSettings:DefaultBranchCode`. You can also set it without a launch profile:

```bash
# Windows PowerShell
$env:BranchSettings__DefaultBranchCode="CAS"; dotnet run --project LibraryManagementSystem
# Linux / macOS
BranchSettings__DefaultBranchCode=CAS dotnet run --project LibraryManagementSystem
```

Or change `"DefaultBranchCode"` in `LibraryManagementSystem/appsettings.json`. The branch code decides which shelves the reception desk and the kiosk lend from, and where returned items are recorded. Staff can also switch desk branch for their session with the **change** link under the menu. That link is handy for testing transfers on one computer.

### First run, the database and seed data

On start-up the app applies the EF Core migrations, creates `library.db` if it doesn't exist, and seeds demo data. **To reset everything**, stop the app, delete `LibraryManagementSystem/library.db`, and run again.

(If you already have a `library.db` from Assignment 2, the app upgrades it: existing items are put in the Darwin City branch and existing borrowers get a card number. Deleting it and starting fresh gives the full demo scenario below.)

### Check that it works

1. The home page loads and shows "Find anything in the stacks".
2. Log in as `reception@library.local` / `Reception123!`. The header shows **Desk branch: Darwin City** (or the branch you chose).
3. Go to `/Kiosk` and sign in with card `C1000001`, PIN `1234`. You should see "Hi Priya".
4. Go to `/Notifications` (logged in as staff). About 10 seconds after start-up the reminder job logs a "due soon" message for Priya and an overdue notice for Sam.

---

## 3. Demo accounts and seeded multi-branch data

**Staff** (same as Assignment 2):

| Role | Email | Password |
|---|---|---|
| Admin | `admin@library.local` | `Admin123!` |
| Reception | `reception@library.local` | `Reception123!` |
| Manager | `manager@library.local` | `Manager123!` |

**Patrons** (for the kiosk and online holds):

| Name | Card | PIN | Notified by | Home |
|---|---|---|---|---|
| Priya Nair | `C1000001` | `1234` | Email + SMS | Darwin City |
| Sam Okafor | `C1000002` | `1234` | Email | Casuarina |
| Lena Fischer | `C1000003` | `2468` | SMS | Palmerston |
| Tom Walker | `C1000004` | `1357` | Email (no phone) | Darwin City |

**Branches:** Darwin City (DAR, 9:00 to 18:00 every day), Casuarina (CAS, 9:00 to 17:00 every day), Palmerston (PAL, 10:00 to 17:00 Mon to Fri). The 17 catalogue items are spread across the three branches.

**Demo scenario** (seeded on a fresh database so every module has something to show):

| Situation | Used to test |
|---|---|
| *The Hobbit* (BK-0001) borrowed by Priya, due tomorrow | "Due soon" reminder |
| *Dune* (BK-0003) borrowed by Sam at the Casuarina kiosk, 5 days overdue | Overdue / fine-accruing notice, late-return fine |
| Lena is waiting for *The Hobbit* and wants to collect it at **Casuarina** | Returning it at Darwin City creates a transfer automatically |
| *Puzzle Cube* (TY-0002) is **Damaged**; Priya is on its waitlist | Admin sets it to Available, and Priya is notified it's ready |
| Tom has an **unpaid $2.00 fine** | Borrowing is blocked until reception records payment |
| Lena's earlier $1.50 fine was paid | Shows as collected revenue in the fine audit |
| *Abbey Road* (MU-0001) is **in transit** Darwin City → Palmerston | Receive it at the Palmerston desk |

---

## 4. How to test each new module

Each walkthrough starts from a fresh database.

**Kiosk (`/Kiosk`)**
1. Type `C1000001` (or use the on-screen keys), then **Next**, then PIN `1234`, then **Sign in**.
2. **Check out items**: scan or type `BK-0004`. It should succeed, with the due date shown on the receipt.
3. Try `BK-0003` (refused: already borrowed) and `TY-0001` (refused: shelved at Casuarina).
4. **My account** shows loans, fines and holds. **Finish & sign out** ends the session. Leaving the screen alone for 2 minutes also signs out.
5. Wrong PINs: after 5 wrong attempts the card is locked for 15 minutes.

**Reservations**
1. Public **Search** for "hobbit". Because it's Borrowed, it shows **Place a hold**. Use card `C1000002`, PIN `1234`. You're number 2 in the queue.
2. As Reception at Darwin City, go to **Return** and enter `BK-0001`. The result page says a transfer was created, because Lena (first in the queue) collects at Casuarina.
3. **Transfers**: **Dispatch** it. Use the **change** link to switch your desk to Casuarina, then **Receive** it. Lena is notified by SMS, and it appears on the **Holds** shelf at Casuarina.
4. Borrowing it for Sam is refused (queue jumping). Borrowing it for Lena works, and her hold becomes Fulfilled.
5. As Admin, edit *Puzzle Cube* and change it from Damaged to Available. Priya is notified it's ready at Palmerston.

**Multi-branch inventory and transfers**: Admin → **Branches** (edit hours, or mark a branch temporarily closed, which the API reflects), **Manage items** (Location column, filter by branch), **Transfers** (request by library code, then dispatch and receive).

**Notifications**: Staff → **Notifications**. Filter by type or channel and expand a row to read the message. **Run reminder check now** runs the automated job immediately. Running it twice doesn't send duplicates.

**Importer**: Admin → **Import**.
1. Upload `docs/sample-import.csv`: 4 rows are valid and 5 have errors (each reason shown). Confirm to import the 4 valid rows.
2. Search the provider for "tolkien", give a record a new library code (e.g. `BK-3001`), then **Add**.

**Reports**: Manager → **Reports**. Choose a report, branch and dates, then **Download CSV** / **Download PDF**.

**API**: see section 5.

**Unit tests**:
```bash
dotnet test
```
There are 38 tests in `LibraryManagementSystem.Tests` for loans and fines across branches, reservations (queue order, transfer for pickup, expiry), transfers, notifications and reminders, PIN lockout, CSV import validation, and the CSV/PDF exports. They use in-memory fake repositories, so no database is needed.

---

## 5. Public REST API

Read-only, JSON, `GET` only. Every request needs the header `X-Api-Key`. The demo key is configured in `appsettings.json` under `ApiSettings:Clients`:

```
X-Api-Key: stacks-demo-key-2026
```

| GET | Purpose |
|---|---|
| `/api/v1/status` | Service health and branches open now |
| `/api/v1/catalogue?search=&type=&branch=&availableOnly=&page=&pageSize=` | Paged catalogue search (`pageSize` max 50) |
| `/api/v1/catalogue/{libraryCode}` | One item |
| `/api/v1/categories` | Types with counts, plus genre / toy type / decade breakdown |
| `/api/v1/branches` | All branches, hours, open now, operating status |
| `/api/v1/branches/{code}/status` | One branch |

Responses: `401` with no or a wrong key, `400` for invalid parameters, `404` for an unknown item or branch, `405` for anything other than GET, `429` when over the rate limit (60 requests per minute per key).

To test:
- open `LibraryManagementSystem/LibraryManagementSystem.http` in Visual Studio and click **Send Request**, or
- run `curl -k -H "X-Api-Key: stacks-demo-key-2026" https://localhost:7050/api/v1/catalogue?search=hobbit`

Documentation for API users is also at `/Home/Developers`.

---

## 6. Configuration (`appsettings.json`)

| Section | Setting | Default | Meaning |
|---|---|---|---|
| `BranchSettings` | `DefaultBranchCode` | `DAR` | Which branch this installation runs at |
| | `TimeZoneId` | `Australia/Darwin` | Used for "open now" |
| `LibrarySettings` | `DefaultLoanPeriodDays`, `FinePerDayOverdue` | 14, 0.50 | From Assignment 2 |
| `NotificationSettings` | `DueSoonDays`, `CheckIntervalMinutes`, `EnableBackgroundChecks` | 2, 60, true | Reminder job |
| `ReservationSettings` | `HoldPickupDays`, `MaxActiveHoldsPerBorrower` | 3, 5 | Holds |
| `KioskSettings` | `IdleTimeoutSeconds`, `MaxPinAttempts`, `PinLockoutMinutes` | 120, 5, 15 | Kiosk security |
| `ApiSettings` | `Clients` (name + key), `PermitsPerMinute` | demo key, 60 | API access |

For a real deployment, API keys should come from user secrets or environment variables, not from the committed `appsettings.json`.

---

## 7. What is simulated

- **Email and SMS**: nothing is actually sent. `SimulatedEmailSender` and `SimulatedSmsSender` write each message to the console log, and `NotificationService` saves it to the `NotificationLogs` table shown on `/Notifications`. A real provider could be plugged in by registering a different `INotificationChannelSender` in `Program.cs`.
- **External metadata provider**: `MockMediaMetadataProvider` returns records from a small built-in sample data set (IDs start with `MOCK-`) after a short artificial delay. No internet access is used. A real API client could replace it through `IMediaMetadataProvider`.
- **Barcode scanning**: USB barcode scanners act like a keyboard (they type the code and press Enter), so the kiosk uses an auto-focused text box. No scanner hardware is needed to test it.

## 8. Security summary

- Staff areas are protected by role-based `[Authorize]`, as in Assignment 2. The kiosk and hold forms are public, but patrons must enter their card number and PIN.
- PINs are hashed with ASP.NET Core Identity's `PasswordHasher`. A card locks after repeated wrong PINs. A wrong card number and a wrong PIN give the same message, so card numbers can't be discovered. PIN forms are rate limited per IP address.
- The kiosk session is server-side, short, and cleared on Finish or when idle.
- API: GET only, API key compared in constant time, per-key rate limiting, a page-size cap, DTOs only (no borrower data), and JSON error responses.
- All forms use anti-forgery tokens. Forms bind to view models, which prevents over-posting.
- CSV import: `.csv` only, 1 MB / 1000-row limit, every row validated, valid rows saved in one batch.
- CSV export: cells starting with `= + - @` are escaped to stop spreadsheet formula injection.
- Known dependency warning: the build shows NuGet warning NU1903 for `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 ([GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q)), a memory-corruption bug in older bundled SQLite versions. The advisory lists no patched version of this package; it recommends SQLite 3.50.2 or later. This is acceptable for a local demo. Only this one advisory is suppressed, in `Directory.Build.props`, because Visual Studio's Package Manager Console otherwise stops `Add-Migration` on the warning. A production deployment should use a database server, or a SQLite package build with the fixed version once one is available.

## 9. Project structure (new parts in **bold**)

```
LibraryManagementSystem.slnx
LibraryManagementSystem/
  **Api/**                      REST API controller, DTOs, API key filter
  Controllers/                  + **Kiosk, Reservations, Transfers, Branches, Desk, Notifications, Import** controllers
  Data/                         ApplicationDbContext (+ new tables), DbInitializer (+ branch/demo seed)
  Migrations/
  Models/                       + **Branch, BranchTransfer, Reservation, NotificationLog**
  Repositories/                 + **Branch, Reservation, Transfer, Notification** repositories
  Services/                     + **reservations, transfers, notifications, reminders, patron PIN, import**
  **Services/Reports/**         report builder, CSV + PDF exporters
  Views/                        + Kiosk (own layout), Reservations, Transfers, Branches, Import, Notifications, Manager/Reports
**LibraryManagementSystem.Tests/**  xUnit tests with in-memory fake repositories
**docs/**                       sample CSV, Assignment 2 README
```

The architecture from Assignment 2 is unchanged. Controllers depend on service and repository interfaces, only repositories touch EF Core, and all concrete classes are registered in `Program.cs` (Dependency Inversion Principle). Following the same rule is what lets the unit tests swap in in-memory repositories.

## 10. Changing the data model (developers)

Migrations are in `LibraryManagementSystem/Migrations` and are applied automatically on start-up. After changing a model class, add a new migration:

```bash
# Visual Studio Package Manager Console
Add-Migration <MigrationName>
# or the .NET CLI (install once: dotnet tool install --global dotnet-ef)
dotnet ef migrations add <MigrationName> --project LibraryManagementSystem
```
