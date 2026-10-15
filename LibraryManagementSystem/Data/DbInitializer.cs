using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Data;

/// <summary>
/// Runs once at startup (see Program.cs) to make sure the database exists,
/// the three roles exist, one demo account per role exists, and - the
/// first time only - sample data exists so markers can see the system
/// working immediately without hand-entering data first.
///
/// Group Project: now also seeds three branches, spreads the catalogue
/// across them, and (on a fresh database only) creates demo loans, fines,
/// reservations and a transfer so every new module has something to show.
/// It is safe to run against an Assignment 2 database too: existing items
/// and borrowers are simply given a branch / card number.
/// </summary>
public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { Roles.Admin, Roles.Reception, Roles.Manager })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        await EnsureUserAsync(userManager, "admin@library.local", "Admin123!", "Alex Admin", Roles.Admin);
        await EnsureUserAsync(userManager, "reception@library.local", "Reception123!", "Robin Reception", Roles.Reception);
        await EnsureUserAsync(userManager, "manager@library.local", "Manager123!", "Morgan Manager", Roles.Manager);

        // ---- Branches (Group Project) ----
        if (!await context.Branches.AnyAsync())
        {
            context.Branches.AddRange(
                new Branch { Code = "DAR", Name = "Darwin City", Address = "Darwin City NT 0800", PhoneNumber = "08 8900 0001", OpeningTime = new TimeOnly(9, 0), ClosingTime = new TimeOnly(18, 0), OpenOnWeekends = true },
                new Branch { Code = "CAS", Name = "Casuarina", Address = "Casuarina NT 0810", PhoneNumber = "08 8900 0002", OpeningTime = new TimeOnly(9, 0), ClosingTime = new TimeOnly(17, 0), OpenOnWeekends = true },
                new Branch { Code = "PAL", Name = "Palmerston", Address = "Palmerston NT 0830", PhoneNumber = "08 8900 0003", OpeningTime = new TimeOnly(10, 0), ClosingTime = new TimeOnly(17, 0), OpenOnWeekends = false }
            );
            await context.SaveChangesAsync();
        }

        var branches = await context.Branches.ToDictionaryAsync(b => b.Code);
        var dar = branches.GetValueOrDefault("DAR") ?? branches.Values.First();
        var cas = branches.GetValueOrDefault("CAS") ?? dar;
        var pal = branches.GetValueOrDefault("PAL") ?? dar;

        if (!await context.Items.AnyAsync())
        {
            context.Items.AddRange(
                // Darwin City
                At(new Book { Name = "The Hobbit", Author = "J.R.R. Tolkien", Genre = "Fantasy", LibraryCode = "BK-0001", Description = "A reluctant hobbit sets out on an unexpected journey." }, dar),
                At(new Book { Name = "Clean Code", Author = "Robert C. Martin", Genre = "Software Engineering", LibraryCode = "BK-0002", Description = "A handbook of agile software craftsmanship." }, dar),
                At(new Book { Name = "Refactoring", Author = "Martin Fowler", Genre = "Software Engineering", LibraryCode = "BK-0004", Description = "Improving the design of existing code." }, dar),
                At(new Book { Name = "The Great Gatsby", Author = "F. Scott Fitzgerald", Genre = "Classic", LibraryCode = "BK-0005" }, dar),
                At(new Music { Name = "Abbey Road", Artist = "The Beatles", Year = 1969, LibraryCode = "MU-0001" }, dar),
                At(new Toy { Name = "Chess Set", ToyType = "Board game", RecommendedAge = 6, LibraryCode = "TY-0003", Description = "Full-size wooden chess set." }, dar),
                // Casuarina
                At(new Book { Name = "Dune", Author = "Frank Herbert", Genre = "Science Fiction", LibraryCode = "BK-0003" }, cas),
                At(new Book { Name = "Matilda", Author = "Roald Dahl", Genre = "Children's", LibraryCode = "BK-0006" }, cas),
                At(new Toy { Name = "Wooden Building Blocks", ToyType = "Construction", RecommendedAge = 3, LibraryCode = "TY-0001", Description = "60-piece natural wood block set." }, cas),
                At(new Toy { Name = "Map of Australia Jigsaw", ToyType = "Puzzle", RecommendedAge = 8, LibraryCode = "TY-0004" }, cas),
                At(new Music { Name = "Thriller", Artist = "Michael Jackson", Year = 1982, LibraryCode = "MU-0002" }, cas),
                At(new Music { Name = "Back to Black", Artist = "Amy Winehouse", Year = 2006, LibraryCode = "MU-0003" }, cas),
                // Palmerston
                At(new Book { Name = "The Book Thief", Author = "Markus Zusak", Genre = "Historical Fiction", LibraryCode = "BK-0007" }, pal),
                At(new Book { Name = "Nineteen Eighty-Four", Author = "George Orwell", Genre = "Dystopian", LibraryCode = "BK-0008" }, pal),
                At(new Toy { Name = "Puzzle Cube", ToyType = "Puzzle", RecommendedAge = 8, LibraryCode = "TY-0002" }, pal),
                At(new Toy { Name = "Junior Science Kit", ToyType = "Educational", RecommendedAge = 10, LibraryCode = "TY-0005" }, pal),
                At(new Music { Name = "Nevermind", Artist = "Nirvana", Year = 1991, LibraryCode = "MU-0004" }, pal)
            );
            await context.SaveChangesAsync();
        }
        else
        {
            // Upgrading an Assignment 2 database: give unassigned items the default branch.
            var unassigned = await context.Items.Where(i => i.HomeBranchId == null || i.CurrentBranchId == null).ToListAsync();
            foreach (var item in unassigned)
            {
                item.HomeBranchId ??= dar.Id;
                item.CurrentBranchId ??= item.HomeBranchId;
            }
            await context.SaveChangesAsync();
        }

        var hasher = new PasswordHasher<Borrower>();

        if (!await context.Borrowers.AnyAsync())
        {
            context.Borrowers.AddRange(
                Patron("Priya Nair", "priya.nair@example.com", "0400 111 222", "C1000001", "1234", NotificationPreference.EmailAndSms, dar, hasher),
                Patron("Sam Okafor", "sam.okafor@example.com", "0400 333 444", "C1000002", "1234", NotificationPreference.Email, cas, hasher),
                Patron("Lena Fischer", "lena.fischer@example.com", "0400 555 666", "C1000003", "2468", NotificationPreference.Sms, pal, hasher),
                Patron("Tom Walker", "tom.walker@example.com", null, "C1000004", "1357", NotificationPreference.Email, dar, hasher)
            );
            await context.SaveChangesAsync();
        }
        else
        {
            // Assignment 2 borrowers had no library card - give them one (PIN is set later by reception).
            var noCard = await context.Borrowers.Where(b => b.CardNumber == null).ToListAsync();
            foreach (var borrower in noCard)
            {
                borrower.CardNumber = $"C{1000000 + borrower.Id}";
                borrower.HomeBranchId ??= dar.Id;
            }
            await context.SaveChangesAsync();
        }

        // Demo activity only on a completely fresh database, so we never
        // invent history on top of real data.
        if (!await context.Loans.AnyAsync())
        {
            await SeedDemoActivityAsync(context, dar, cas, pal);
        }
    }

    private static async Task SeedDemoActivityAsync(ApplicationDbContext context, Branch dar, Branch cas, Branch pal)
    {
        var items = await context.Items.ToDictionaryAsync(i => i.LibraryCode);
        var people = await context.Borrowers.ToDictionaryAsync(b => b.CardNumber!);
        if (!items.ContainsKey("BK-0001") || !people.ContainsKey("C1000001"))
        {
            return; // not the standard seed data - skip the demo scenario
        }

        var today = DateTime.UtcNow;
        var priya = people["C1000001"];
        var sam = people["C1000002"];
        var lena = people["C1000003"];
        var tom = people["C1000004"];

        // 1. Due tomorrow -> the reminder job sends a "due soon" notice.
        context.Loans.Add(new Loan { Item = items["BK-0001"], Borrower = priya, BorrowedDate = today.AddDays(-13), DueDate = today.AddDays(1), BorrowBranchId = dar.Id, BorrowChannel = "Desk" });
        items["BK-0001"].Status = ItemStatus.Borrowed;

        // 2. Five days overdue -> the reminder job sends an "overdue, fine accruing" notice.
        context.Loans.Add(new Loan { Item = items["BK-0003"], Borrower = sam, BorrowedDate = today.AddDays(-19), DueDate = today.AddDays(-5), BorrowBranchId = cas.Id, BorrowChannel = "Kiosk" });
        items["BK-0003"].Status = ItemStatus.Borrowed;

        // 3. Borrowed at the kiosk, not due for a while.
        context.Loans.Add(new Loan { Item = items["BK-0008"], Borrower = lena, BorrowedDate = today.AddDays(-3), DueDate = today.AddDays(11), BorrowBranchId = pal.Id, BorrowChannel = "Kiosk" });
        items["BK-0008"].Status = ItemStatus.Borrowed;

        // 4. Returned 3 days late, fine paid (shows up as collected revenue).
        context.Loans.Add(new Loan { Item = items["BK-0002"], Borrower = lena, BorrowedDate = today.AddDays(-40), DueDate = today.AddDays(-26), ReturnedDate = today.AddDays(-23), FineAmount = 1.50m, FinePaid = true, FinePaidDate = today.AddDays(-22), ConditionOnReturn = ItemStatus.Available, BorrowBranchId = dar.Id, ReturnBranchId = dar.Id, BorrowChannel = "Desk" });

        // 5. Returned 4 days late, fine NOT paid - Tom can't borrow until reception records payment.
        context.Loans.Add(new Loan { Item = items["BK-0006"], Borrower = tom, BorrowedDate = today.AddDays(-30), DueDate = today.AddDays(-16), ReturnedDate = today.AddDays(-12), FineAmount = 2.00m, ConditionOnReturn = ItemStatus.Available, BorrowBranchId = cas.Id, ReturnBranchId = cas.Id, BorrowChannel = "Desk" });

        // 6. Returned on time.
        context.Loans.Add(new Loan { Item = items["MU-0002"], Borrower = priya, BorrowedDate = today.AddDays(-25), DueDate = today.AddDays(-11), ReturnedDate = today.AddDays(-15), ConditionOnReturn = ItemStatus.Available, BorrowBranchId = cas.Id, ReturnBranchId = cas.Id, BorrowChannel = "Desk" });

        // 7. A damaged item with someone waiting for it to be repaired.
        items["TY-0002"].Status = ItemStatus.Damaged;
        context.Reservations.Add(new Reservation { Item = items["TY-0002"], Borrower = priya, PickupBranchId = pal.Id, CreatedAt = today.AddDays(-2), Status = ReservationStatus.Waiting });

        // 8. Someone waiting for The Hobbit, to collect at a DIFFERENT branch
        //    (returning it at Darwin City will automatically request a transfer).
        context.Reservations.Add(new Reservation { Item = items["BK-0001"], Borrower = lena, PickupBranchId = cas.Id, CreatedAt = today.AddDays(-1), Status = ReservationStatus.Waiting });

        // 9. A transfer already on its way from Darwin City to Palmerston.
        context.BranchTransfers.Add(new BranchTransfer { Item = items["MU-0001"], FromBranchId = dar.Id, ToBranchId = pal.Id, Status = TransferStatus.InTransit, Reason = "Requested for Palmerston music display", RequestedAt = today.AddDays(-1), RequestedBy = "Robin Reception", DispatchedAt = today.AddHours(-20), DispatchedBy = "Robin Reception" });

        await context.SaveChangesAsync();
    }

    private static Item At(Item item, Branch branch)
    {
        item.HomeBranchId = branch.Id;
        item.CurrentBranchId = branch.Id;
        return item;
    }

    private static Borrower Patron(string name, string email, string? phone, string card, string pin,
        NotificationPreference preference, Branch home, PasswordHasher<Borrower> hasher)
    {
        var borrower = new Borrower
        {
            FullName = name,
            Email = email,
            PhoneNumber = phone,
            CardNumber = card,
            NotificationPreference = preference,
            HomeBranchId = home.Id
        };
        borrower.PinHash = hasher.HashPassword(borrower, pin);
        return borrower;
    }

    private static async Task EnsureUserAsync(UserManager<ApplicationUser> userManager, string email, string password, string displayName, string role)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }
}
