using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Data;

/// <summary>
/// The single EF Core context for the whole application. It inherits from
/// IdentityDbContext so the three staff roles (Admin/Reception/Manager) can
/// log in, and it also exposes the library's own domain tables.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Item> Items => Set<Item>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Toy> Toys => Set<Toy>();
    public DbSet<Music> MusicItems => Set<Music>();
    public DbSet<Borrower> Borrowers => Set<Borrower>();
    public DbSet<Loan> Loans => Set<Loan>();

    // --- Group Project additions ---
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<BranchTransfer> BranchTransfers => Set<BranchTransfer>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Table-Per-Hierarchy: Book, Toy and Music all live in one "Items"
        // table, distinguished by a "ItemType" discriminator column. This
        // is what makes the Item superclass / three subclasses (1a) map
        // cleanly onto a relational database.
        builder.Entity<Item>()
            .ToTable("Items")
            .HasDiscriminator<string>("ItemDiscriminator")
            .HasValue<Book>("Book")
            .HasValue<Toy>("Toy")
            .HasValue<Music>("Music");

        builder.Entity<Item>()
            .HasIndex(i => i.LibraryCode)
            .IsUnique();

        builder.Entity<Item>()
            .Property(i => i.Status)
            .HasConversion<string>();

        builder.Entity<Loan>()
            .Property(l => l.ConditionOnReturn)
            .HasConversion<string>();

        builder.Entity<Loan>()
            .HasOne(l => l.Item)
            .WithMany(i => i.Loans)
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Loan>()
            .HasOne(l => l.Borrower)
            .WithMany(b => b.Loans)
            .HasForeignKey(l => l.BorrowerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Book>().Property(b => b.Author).HasMaxLength(150);
        builder.Entity<Music>().Property(m => m.Artist).HasMaxLength(150);
        builder.Entity<Toy>().Property(t => t.ToyType).HasMaxLength(80);

        // ------------------------------------------------------------------
        // Group Project: multi-branch inventory
        // ------------------------------------------------------------------
        builder.Entity<Branch>()
            .HasIndex(b => b.Code)
            .IsUnique();

        builder.Entity<Branch>()
            .Property(b => b.OperatingStatus)
            .HasConversion<string>();

        // An item has two separate links to Branch: who owns it, and where
        // it physically is. Restrict deletes so a branch that still holds
        // stock can't be removed by accident.
        builder.Entity<Item>()
            .HasOne(i => i.HomeBranch)
            .WithMany()
            .HasForeignKey(i => i.HomeBranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Item>()
            .HasOne(i => i.CurrentBranch)
            .WithMany()
            .HasForeignKey(i => i.CurrentBranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Borrower>()
            .HasOne(b => b.HomeBranch)
            .WithMany()
            .HasForeignKey(b => b.HomeBranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique, but nullable - SQLite allows many NULLs in a unique index,
        // so Assignment 2 rows without a card number still migrate cleanly.
        builder.Entity<Borrower>()
            .HasIndex(b => b.CardNumber)
            .IsUnique();

        builder.Entity<Loan>()
            .HasOne(l => l.BorrowBranch)
            .WithMany()
            .HasForeignKey(l => l.BorrowBranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Loan>()
            .HasOne(l => l.ReturnBranch)
            .WithMany()
            .HasForeignKey(l => l.ReturnBranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<BranchTransfer>(t =>
        {
            t.Property(x => x.Status).HasConversion<string>();
            t.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
            t.HasOne(x => x.FromBranch).WithMany().HasForeignKey(x => x.FromBranchId).OnDelete(DeleteBehavior.Restrict);
            t.HasOne(x => x.ToBranch).WithMany().HasForeignKey(x => x.ToBranchId).OnDelete(DeleteBehavior.Restrict);
        });

        // ------------------------------------------------------------------
        // Group Project: reservations / waitlist
        // ------------------------------------------------------------------
        builder.Entity<Reservation>(r =>
        {
            r.Property(x => x.Status).HasConversion<string>();
            r.HasOne(x => x.Item).WithMany(i => i.Reservations).HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
            r.HasOne(x => x.Borrower).WithMany(b => b.Reservations).HasForeignKey(x => x.BorrowerId).OnDelete(DeleteBehavior.Restrict);
            r.HasOne(x => x.PickupBranch).WithMany().HasForeignKey(x => x.PickupBranchId).OnDelete(DeleteBehavior.Restrict);
            // The queue is always read as "this item's holds, oldest first".
            r.HasIndex(x => new { x.ItemId, x.Status, x.CreatedAt });
        });

        // ------------------------------------------------------------------
        // Group Project: notification log
        // ------------------------------------------------------------------
        builder.Entity<NotificationLog>(n =>
        {
            n.Property(x => x.Type).HasConversion<string>().HasMaxLength(40);
            n.Property(x => x.Channel).HasConversion<string>().HasMaxLength(10);
            n.Property(x => x.DeliveryStatus).HasConversion<string>().HasMaxLength(20);
            n.Property(x => x.Recipient).HasMaxLength(150);
            n.Property(x => x.Subject).HasMaxLength(200);
            n.Property(x => x.Body).HasMaxLength(2000);
            // Deleting a borrower keeps their message history, just unlinked.
            n.HasOne(x => x.Borrower).WithMany().HasForeignKey(x => x.BorrowerId).OnDelete(DeleteBehavior.SetNull);
            n.HasIndex(x => new { x.Type, x.LoanId, x.CreatedAt });
        });
    }
}
