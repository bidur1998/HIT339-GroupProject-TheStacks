using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;
using LibraryManagementSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LibraryManagementSystem.Tests.TestSupport;

/// <summary>A clock the tests can set and move forward.</summary>
public class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 14, 1, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now = Now.Add(by);
}

/// <summary>
/// An in-memory stand-in for the database. Because every service depends on
/// repository INTERFACES (Dependency Inversion), the tests can plug these
/// fakes in and check the business rules without SQLite or EF Core.
/// Navigation properties are re-linked on every read, like EF would do.
/// </summary>
public class InMemoryLibrary
{
    public List<Branch> Branches { get; } = new();
    public List<Item> Items { get; } = new();
    public List<Borrower> Borrowers { get; } = new();
    public List<Loan> Loans { get; } = new();
    public List<Reservation> Reservations { get; } = new();
    public List<BranchTransfer> Transfers { get; } = new();
    public List<NotificationLog> Notifications { get; } = new();

    public TestClock Clock { get; } = new();

    private int _nextId = 1;
    public int NextId() => _nextId++;

    public void Link()
    {
        foreach (var i in Items)
        {
            i.HomeBranch = Branches.FirstOrDefault(b => b.Id == i.HomeBranchId);
            i.CurrentBranch = Branches.FirstOrDefault(b => b.Id == i.CurrentBranchId);
            i.Loans = Loans.Where(l => l.ItemId == i.Id).ToList();
            i.Reservations = Reservations.Where(r => r.ItemId == i.Id).ToList();
        }
        foreach (var b in Borrowers)
        {
            b.HomeBranch = Branches.FirstOrDefault(x => x.Id == b.HomeBranchId);
            b.Loans = Loans.Where(l => l.BorrowerId == b.Id).ToList();
            b.Reservations = Reservations.Where(r => r.BorrowerId == b.Id).ToList();
        }
        foreach (var l in Loans)
        {
            l.Item = Items.First(i => i.Id == l.ItemId);
            l.Borrower = Borrowers.First(b => b.Id == l.BorrowerId);
            l.BorrowBranch = Branches.FirstOrDefault(b => b.Id == l.BorrowBranchId);
            l.ReturnBranch = Branches.FirstOrDefault(b => b.Id == l.ReturnBranchId);
        }
        foreach (var r in Reservations)
        {
            r.Item = Items.First(i => i.Id == r.ItemId);
            r.Borrower = Borrowers.First(b => b.Id == r.BorrowerId);
            r.PickupBranch = Branches.First(b => b.Id == r.PickupBranchId);
        }
        foreach (var t in Transfers)
        {
            t.Item = Items.First(i => i.Id == t.ItemId);
            t.FromBranch = Branches.First(b => b.Id == t.FromBranchId);
            t.ToBranch = Branches.First(b => b.Id == t.ToBranchId);
        }
        foreach (var n in Notifications)
        {
            n.Borrower = Borrowers.FirstOrDefault(b => b.Id == n.BorrowerId);
        }
    }

    // ---- Quick builders for test data ----

    public Branch AddBranch(string code, string name)
    {
        var b = new Branch { Id = NextId(), Code = code, Name = name, OpeningTime = new TimeOnly(9, 0), ClosingTime = new TimeOnly(17, 0) };
        Branches.Add(b);
        return b;
    }

    public Book AddBook(string code, string name, Branch at, ItemStatus status = ItemStatus.Available)
    {
        var item = new Book { Id = NextId(), LibraryCode = code, Name = name, Author = "Test Author", Status = status, HomeBranchId = at.Id, CurrentBranchId = at.Id };
        Items.Add(item);
        Link();
        return item;
    }

    public Borrower AddBorrower(string name, NotificationPreference preference = NotificationPreference.Email, string? phone = null)
    {
        var b = new Borrower
        {
            Id = NextId(),
            FullName = name,
            Email = name.Replace(" ", ".").ToLower() + "@example.com",
            PhoneNumber = phone,
            CardNumber = "C" + (1000000 + _nextId),
            NotificationPreference = preference
        };
        Borrowers.Add(b);
        Link();
        return b;
    }

    // ---- Wiring the real services to the fakes ----

    public IItemRepository ItemRepo => new FakeItemRepository(this);
    public IBorrowerRepository BorrowerRepo => new FakeBorrowerRepository(this);
    public ILoanRepository LoanRepo => new FakeLoanRepository(this);
    public IBranchRepository BranchRepo => new FakeBranchRepository(this);
    public IReservationRepository ReservationRepo => new FakeReservationRepository(this);
    public ITransferRepository TransferRepo => new FakeTransferRepository(this);
    public INotificationRepository NotificationRepo => new FakeNotificationRepository(this);

    public LibrarySettings LibrarySettings { get; } = new() { DefaultLoanPeriodDays = 14, FinePerDayOverdue = 0.50m };
    public ReservationSettings ReservationSettings { get; } = new() { HoldPickupDays = 3, MaxActiveHoldsPerBorrower = 5 };
    public NotificationSettings NotificationSettings { get; } = new() { DueSoonDays = 2 };
    public KioskSettings KioskSettings { get; } = new() { MaxPinAttempts = 3, PinLockoutMinutes = 15 };

    public IFineCalculator FineCalculator => new FineCalculator(Options.Create(LibrarySettings));

    public INotificationService NotificationService => new NotificationService(
        new INotificationChannelSender[]
        {
            new SimulatedEmailSender(NullLogger<SimulatedEmailSender>.Instance),
            new SimulatedSmsSender(NullLogger<SimulatedSmsSender>.Instance)
        },
        NotificationRepo, Clock, NullLogger<NotificationService>.Instance);

    public IReservationService ReservationService => new ReservationService(
        ReservationRepo, ItemRepo, BorrowerRepo, BranchRepo, TransferRepo, NotificationService, Clock,
        Options.Create(ReservationSettings));

    public ILoanService LoanService => new LoanService(
        ItemRepo, BorrowerRepo, LoanRepo, FineCalculator, ReservationService, TransferRepo, NotificationService, Clock);

    public ITransferService TransferService => new TransferService(TransferRepo, ItemRepo, BranchRepo, ReservationService, Clock);

    public IReminderService ReminderService => new ReminderService(
        LoanRepo, NotificationRepo, NotificationService, ReservationService, FineCalculator, Clock,
        Options.Create(NotificationSettings), Options.Create(LibrarySettings));

    public IPatronAuthService PatronAuth => new PatronAuthService(
        BorrowerRepo, new PasswordHasher<Borrower>(), Clock, Options.Create(KioskSettings));

    public IPatronAccountService PatronAccounts => new PatronAccountService(BorrowerRepo, ReservationService, FineCalculator, Clock);

    public ICatalogueImportService Importer => new CatalogueImportService(ItemRepo, BranchRepo, new InstantMetadataProvider());
}

/// <summary>Same data as the mock provider but without the artificial delay, so tests run fast.</summary>
public class InstantMetadataProvider : IMediaMetadataProvider
{
    private readonly MockMediaMetadataProvider _inner = new();
    public string ProviderName => _inner.ProviderName;
    public Task<List<ExternalMediaRecord>> SearchAsync(string query, CancellationToken cancellationToken = default) => _inner.SearchAsync(query, cancellationToken);
    public Task<ExternalMediaRecord?> GetByIdAsync(string externalId, CancellationToken cancellationToken = default) => _inner.GetByIdAsync(externalId, cancellationToken);
}

// ------------------------------------------------------------------------
// Fake repositories
// ------------------------------------------------------------------------

public class FakeItemRepository : IItemRepository
{
    private readonly InMemoryLibrary _db;
    public FakeItemRepository(InMemoryLibrary db) => _db = db;

    private List<Item> Linked() { _db.Link(); return _db.Items; }

    public Task<List<Item>> GetAllAsync() => Task.FromResult(Linked().OrderBy(i => i.Name).ToList());
    public Task<Item?> GetByIdAsync(int id) => Task.FromResult(Linked().FirstOrDefault(i => i.Id == id));
    public Task<Item?> GetByLibraryCodeAsync(string code) =>
        Task.FromResult(Linked().FirstOrDefault(i => i.LibraryCode.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase)));

    public Task<List<Item>> SearchAsync(string? query, string? typeFilter, bool availableOnly, int? branchId = null)
    {
        IEnumerable<Item> items = Linked();
        if (!string.IsNullOrWhiteSpace(query)) items = items.Where(i => i.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || i.LibraryCode.Contains(query, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(typeFilter) && typeFilter != "All") items = items.Where(i => i.ItemType == typeFilter);
        if (availableOnly) items = items.Where(i => i.Status == ItemStatus.Available);
        if (branchId.HasValue) items = items.Where(i => i.CurrentBranchId == branchId);
        return Task.FromResult(items.OrderBy(i => i.Name).ToList());
    }

    public Task AddAsync(Item item) { item.Id = _db.NextId(); _db.Items.Add(item); _db.Link(); return Task.CompletedTask; }
    public Task UpdateAsync(Item item) { _db.Link(); return Task.CompletedTask; }
    public Task DeleteAsync(Item item) { _db.Items.Remove(item); return Task.CompletedTask; }
    public Task<bool> LibraryCodeExistsAsync(string code, int excludeId = 0) =>
        Task.FromResult(_db.Items.Any(i => i.LibraryCode.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase) && i.Id != excludeId));
    public Task AddRangeAsync(IEnumerable<Item> items) { foreach (var i in items) { i.Id = _db.NextId(); _db.Items.Add(i); } _db.Link(); return Task.CompletedTask; }
    public Task<HashSet<string>> GetExistingLibraryCodesAsync(IEnumerable<string> codes)
    {
        var wanted = codes.Select(c => c.ToUpperInvariant()).ToHashSet();
        return Task.FromResult(_db.Items.Select(i => i.LibraryCode.ToUpperInvariant()).Where(wanted.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase));
    }
    public Task<bool> HasHistoryAsync(int itemId) =>
        Task.FromResult(_db.Loans.Any(l => l.ItemId == itemId) || _db.Reservations.Any(r => r.ItemId == itemId) || _db.Transfers.Any(t => t.ItemId == itemId));
}

public class FakeBorrowerRepository : IBorrowerRepository
{
    private readonly InMemoryLibrary _db;
    public FakeBorrowerRepository(InMemoryLibrary db) => _db = db;

    private List<Borrower> Linked() { _db.Link(); return _db.Borrowers; }

    public Task<List<Borrower>> GetAllAsync() => Task.FromResult(Linked().OrderBy(b => b.FullName).ToList());
    public Task<Borrower?> GetByIdAsync(int id) => Task.FromResult(Linked().FirstOrDefault(b => b.Id == id));
    public Task AddAsync(Borrower borrower) { borrower.Id = _db.NextId(); _db.Borrowers.Add(borrower); _db.Link(); return Task.CompletedTask; }
    public Task UpdateAsync(Borrower borrower) => Task.CompletedTask;
    public Task DeleteAsync(Borrower borrower) { _db.Borrowers.Remove(borrower); return Task.CompletedTask; }
    public Task<Borrower?> GetByCardNumberAsync(string card) =>
        Task.FromResult(Linked().FirstOrDefault(b => string.Equals(b.CardNumber, card.Trim(), StringComparison.OrdinalIgnoreCase)));
    public Task<bool> CardNumberExistsAsync(string card) =>
        Task.FromResult(_db.Borrowers.Any(b => string.Equals(b.CardNumber, card.Trim(), StringComparison.OrdinalIgnoreCase)));
    public Task<bool> HasHistoryAsync(int borrowerId) =>
        Task.FromResult(_db.Loans.Any(l => l.BorrowerId == borrowerId) || _db.Reservations.Any(r => r.BorrowerId == borrowerId));
}

public class FakeLoanRepository : ILoanRepository
{
    private readonly InMemoryLibrary _db;
    public FakeLoanRepository(InMemoryLibrary db) => _db = db;

    private List<Loan> Linked() { _db.Link(); return _db.Loans; }

    public Task<List<Loan>> GetAllAsync() => Task.FromResult(Linked().OrderByDescending(l => l.BorrowedDate).ToList());
    public Task<Loan?> GetActiveLoanForItemAsync(int itemId) => Task.FromResult(Linked().FirstOrDefault(l => l.ItemId == itemId && l.ReturnedDate == null));
    public Task<Loan?> GetByIdAsync(int id) => Task.FromResult(Linked().FirstOrDefault(l => l.Id == id));
    public Task AddAsync(Loan loan) { loan.Id = _db.NextId(); _db.Loans.Add(loan); _db.Link(); return Task.CompletedTask; }
    public Task UpdateAsync(Loan loan) { _db.Link(); return Task.CompletedTask; }
    public Task<List<Loan>> GetActiveLoansAsync() => Task.FromResult(Linked().Where(l => l.ReturnedDate == null).OrderBy(l => l.DueDate).ToList());
}

public class FakeBranchRepository : IBranchRepository
{
    private readonly InMemoryLibrary _db;
    public FakeBranchRepository(InMemoryLibrary db) => _db = db;

    public Task<List<Branch>> GetAllAsync() => Task.FromResult(_db.Branches.OrderBy(b => b.Name).ToList());
    public Task<Branch?> GetByIdAsync(int id) => Task.FromResult(_db.Branches.FirstOrDefault(b => b.Id == id));
    public Task<Branch?> GetByCodeAsync(string code) => Task.FromResult(_db.Branches.FirstOrDefault(b => b.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase)));
    public Task<bool> CodeExistsAsync(string code, int excludeId = 0) => Task.FromResult(_db.Branches.Any(b => b.Code.Equals(code, StringComparison.OrdinalIgnoreCase) && b.Id != excludeId));
    public Task AddAsync(Branch branch) { branch.Id = _db.NextId(); _db.Branches.Add(branch); return Task.CompletedTask; }
    public Task UpdateAsync(Branch branch) => Task.CompletedTask;
}

public class FakeReservationRepository : IReservationRepository
{
    private readonly InMemoryLibrary _db;
    public FakeReservationRepository(InMemoryLibrary db) => _db = db;

    private List<Reservation> Linked() { _db.Link(); return _db.Reservations; }

    public Task<Reservation?> GetByIdAsync(int id) => Task.FromResult(Linked().FirstOrDefault(r => r.Id == id));
    public Task<List<Reservation>> GetActiveQueueForItemAsync(int itemId) =>
        Task.FromResult(Linked().Where(r => r.ItemId == itemId && r.IsActive).OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).ToList());
    public Task<List<Reservation>> GetActiveForBorrowerAsync(int borrowerId) =>
        Task.FromResult(Linked().Where(r => r.BorrowerId == borrowerId && r.IsActive).OrderBy(r => r.CreatedAt).ToList());
    public Task<List<Reservation>> GetAllActiveAsync() => Task.FromResult(Linked().Where(r => r.IsActive).ToList());
    public Task<List<Reservation>> GetExpiredReadyHoldsAsync(DateTime nowUtc) =>
        Task.FromResult(Linked().Where(r => r.Status == ReservationStatus.ReadyForPickup && r.HoldExpiresAt < nowUtc).ToList());
    public Task<List<Reservation>> GetAllAsync() => Task.FromResult(Linked().ToList());
    public Task AddAsync(Reservation r) { r.Id = _db.NextId(); _db.Reservations.Add(r); _db.Link(); return Task.CompletedTask; }
    public Task UpdateAsync(Reservation r) => Task.CompletedTask;
}

public class FakeTransferRepository : ITransferRepository
{
    private readonly InMemoryLibrary _db;
    public FakeTransferRepository(InMemoryLibrary db) => _db = db;

    private List<BranchTransfer> Linked() { _db.Link(); return _db.Transfers; }

    public Task<List<BranchTransfer>> GetAllAsync() => Task.FromResult(Linked().OrderByDescending(t => t.RequestedAt).ToList());
    public Task<BranchTransfer?> GetByIdAsync(int id) => Task.FromResult(Linked().FirstOrDefault(t => t.Id == id));
    public Task<BranchTransfer?> GetOpenForItemAsync(int itemId) => Task.FromResult(Linked().FirstOrDefault(t => t.ItemId == itemId && t.IsOpen));
    public Task AddAsync(BranchTransfer t) { t.Id = _db.NextId(); _db.Transfers.Add(t); _db.Link(); return Task.CompletedTask; }
    public Task UpdateAsync(BranchTransfer t) => Task.CompletedTask;
}

public class FakeNotificationRepository : INotificationRepository
{
    private readonly InMemoryLibrary _db;
    public FakeNotificationRepository(InMemoryLibrary db) => _db = db;

    public Task AddAsync(NotificationLog log) { log.Id = _db.NextId(); _db.Notifications.Add(log); return Task.CompletedTask; }
    public Task<List<NotificationLog>> SearchAsync(NotificationType? type, NotificationChannel? channel, string? q, int take = 200)
    {
        _db.Link();
        IEnumerable<NotificationLog> logs = _db.Notifications;
        if (type.HasValue) logs = logs.Where(n => n.Type == type);
        if (channel.HasValue) logs = logs.Where(n => n.Channel == channel);
        if (!string.IsNullOrWhiteSpace(q)) logs = logs.Where(n => n.Recipient.Contains(q, StringComparison.OrdinalIgnoreCase) || n.Subject.Contains(q, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(logs.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Take(take).ToList());
    }
    public Task<bool> ExistsForLoanAsync(NotificationType type, int loanId, DateTime since) =>
        Task.FromResult(_db.Notifications.Any(n => n.Type == type && n.LoanId == loanId && n.CreatedAt >= since));
    public Task<int> CountAsync() => Task.FromResult(_db.Notifications.Count);
}
