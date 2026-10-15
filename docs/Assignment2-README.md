# The Stacks — Library Management System

This is my submission for Coding Assignment 2 in Distributed Development. The brief was to
build a library management system, so I built a web app (ASP.NET Core MVC) that lets an
admin manage the catalogue, reception staff handle borrowing/returning and fines, a manager
view statistics, and the public search the catalogue from their phone.

## Why I built it this way

I decided to go with ASP.NET Core MVC, EF Core and ASP.NET Core Identity since that's what
we've been covering in the unit, and I wanted to actually apply what was in the lecture
slides rather than just bolt something together. A few specific choices:

- **SQLite instead of SQL Server** for the database, mainly so the whole project runs with
  nothing extra to install — the database is just one file that gets created automatically
  the first time you run it. Made it a lot easier to test on my own laptop without setting
  up a database server.
- **One `Item` superclass with `Book`, `Toy` and `Music` inheriting from it**, exactly as
  the brief asked for. I mapped all three into a single database table using what EF Core
  calls "Table-Per-Hierarchy" — it felt like the cleanest way to have three different item
  types but still be able to list, search and count "all items" in one place.
- **Interfaces everywhere between the layers** (controllers talk to service interfaces,
  services talk to repository interfaces, and only the repositories know EF Core exists).
  I based this on the Dependency Inversion Principle reading we were given for this unit —
  the idea that high-level code shouldn't depend directly on low-level details, both should
  depend on an abstraction in between. It also matches how the Dependency Injection slides
  showed registering an interface + implementation pair in `Program.cs`.
- **ASP.NET Core Identity with roles** for the three staff logins (Admin, Reception,
  Manager), because it already handles password hashing and account lockout properly
  rather than me trying to write that myself.

## How the project is structured

```
Controllers  --->  Service interfaces  --->  Repository interfaces  --->  EF Core DbContext
(Admin/Reception/    (ILoanService,           (IItemRepository,
 Manager/Public)      IStatisticsService,       IBorrowerRepository,
                       IFineCalculator)          ILoanRepository)
```

- **Models/** — `Item` (abstract) plus `Book`, `Toy`, `Music`, plus `Borrower` and `Loan`
  for the people and the borrow/return transactions.
- **Data/** — the `ApplicationDbContext` and a `DbInitializer` that seeds three demo
  accounts and some sample catalogue items the first time the app runs, so I (and whoever
  is marking this) don't have to type in test data by hand.
- **Repositories/** — the only layer that actually talks to the database.
- **Services/** — where the actual business rules live: calculating fines, checking an
  item is available before it can be borrowed, and building the manager's statistics.
- **Controllers/** — one set per account type, each locked down with
  `[Authorize(Roles = "...")]` so, for example, someone logged in as Reception can't get
  into the Admin screens just by typing the URL in.

## What I did for each requirement

**1. Admin — item CRUD.** `ItemsController` (Admin role only). `Item` is the superclass,
`Book`/`Toy`/`Music` inherit from it. Every item has a name, description, library code and
a status (Available/Borrowed/Damaged/Destroyed). I used one Create/Edit form for all three
types with the extra fields (author/genre, artist/year, type/age) shown or hidden depending
on which type you pick — felt neater than building three near-identical screens.

**2. Reception.** `ReceptionController` handles borrowing and returning by library code.
Borrowing checks the item is actually available and sets a due date. Returning works out
if it's late and calculates a fine automatically, and also records what condition the item
came back in — so if reception marks something as damaged on return, its status actually
updates to Damaged. `BorrowersController` handles full CRUD on borrower details. I also
added a rule that a borrower with unpaid fines can't borrow another item, which wasn't
strictly asked for but felt like a realistic thing a real library would want.

**3. Manager — statistics.** `ManagerController` shows borrowing stats (total loans,
what's currently out, what's overdue, most-borrowed items), item status stats (how many are
available/borrowed/damaged/destroyed, broken down by type), and fine stats (issued,
collected, outstanding).

**4. Public search.** `SearchController` doesn't require logging in at all, since members
of the public shouldn't need an account just to check if something's on the shelf. It
searches by title, library code, author or artist, and can be filtered by type or
availability. I built the layout mobile-first with plain CSS (no Bootstrap or other
framework) so it genuinely works on a phone screen rather than just technically resizing.

## Security

I tried to apply what we covered in the Security and Common Vulnerabilities lectures:

- Every form includes an anti-forgery token to protect against CSRF.
- All the pages use Razor's normal `@` syntax, which automatically encodes output, so I'm
  not accidentally leaving an XSS hole anywhere.
- All database access goes through EF Core/LINQ rather than raw SQL, so there's no SQL
  injection risk.
- The login page only redirects back to a page within the site itself, to avoid open
  redirect attacks.
- Passwords are never handled by my own code — Identity hashes them, and locks an account
  out after five failed attempts.

## How to run it

1. Open `LibraryManagementSystem.csproj` in Visual Studio.
2. Let it restore the NuGet packages (happens automatically).
3. In Tools → NuGet Package Manager → Package Manager Console, run:
   ```
   Add-Migration InitialCreate
   Update-Database
   ```
4. Press the green Run button.
5. Log in with one of the seeded demo accounts to try each role:
   - Admin: `admin@library.local` / `Admin123!`
   - Reception: `reception@library.local` / `Reception123!`
   - Manager: `manager@library.local` / `Manager123!`

## What I'd add if I had more time

- A "mark fine as paid" button — right now fines are calculated and tracked, but there's
  no screen to actually record that a borrower has paid one off.
- Letting the public reserve an item online instead of just seeing whether it's available.
- More automated tests around the fine calculation and the borrowing rules, since those are
  the parts with the most actual logic in them.