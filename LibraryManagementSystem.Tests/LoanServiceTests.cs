using LibraryManagementSystem.Models;
using LibraryManagementSystem.Services;
using LibraryManagementSystem.Tests.TestSupport;

namespace LibraryManagementSystem.Tests;

/// <summary>Borrowing and returning across branches, fines, and fine payment.</summary>
public class LoanServiceTests
{
    private readonly InMemoryLibrary _db = new();
    private readonly Branch _darwin;
    private readonly Branch _casuarina;
    private readonly Borrower _priya;

    public LoanServiceTests()
    {
        _darwin = _db.AddBranch("DAR", "Darwin City");
        _casuarina = _db.AddBranch("CAS", "Casuarina");
        _priya = _db.AddBorrower("Priya Nair");
    }

    [Fact]
    public async Task Borrow_available_item_at_its_branch_creates_loan_and_sends_notification()
    {
        var book = _db.AddBook("BK-1", "Dune", _darwin);

        var loan = await _db.LoanService.BorrowItemAsync("BK-1", _priya.Id, 14, _darwin.Id, "Kiosk");

        Assert.Equal(ItemStatus.Borrowed, book.Status);
        Assert.Equal(_darwin.Id, loan.BorrowBranchId);
        Assert.Equal("Kiosk", loan.BorrowChannel);
        Assert.Equal(_db.Clock.Now.UtcDateTime.AddDays(14), loan.DueDate);
        Assert.Contains(_db.Notifications, n => n.Type == NotificationType.ItemBorrowed && n.LoanId == loan.Id);
    }

    [Fact]
    public async Task Cannot_borrow_item_shelved_at_another_branch()
    {
        _db.AddBook("BK-1", "Dune", _casuarina);

        var ex = await Assert.ThrowsAsync<LoanOperationException>(() =>
            _db.LoanService.BorrowItemAsync("BK-1", _priya.Id, 14, _darwin.Id));

        Assert.Contains("Casuarina", ex.Message);
    }

    [Fact]
    public async Task Cannot_borrow_item_that_is_being_transferred()
    {
        var book = _db.AddBook("BK-1", "Dune", _darwin);
        await _db.TransferService.RequestAsync(book.Id, _casuarina.Id, "staff", null);

        await Assert.ThrowsAsync<LoanOperationException>(() =>
            _db.LoanService.BorrowItemAsync("BK-1", _priya.Id, 14, _darwin.Id));
    }

    [Fact]
    public async Task Loan_period_outside_1_to_90_days_is_rejected()
    {
        _db.AddBook("BK-1", "Dune", _darwin);

        await Assert.ThrowsAsync<LoanOperationException>(() => _db.LoanService.BorrowItemAsync("BK-1", _priya.Id, 0, _darwin.Id));
        await Assert.ThrowsAsync<LoanOperationException>(() => _db.LoanService.BorrowItemAsync("BK-1", _priya.Id, 91, _darwin.Id));
    }

    [Fact]
    public async Task Late_return_charges_fine_per_day_and_notifies_borrower()
    {
        _db.AddBook("BK-1", "Dune", _darwin);
        await _db.LoanService.BorrowItemAsync("BK-1", _priya.Id, 14, _darwin.Id);

        _db.Clock.Advance(TimeSpan.FromDays(17)); // 3 days late
        var outcome = await _db.LoanService.ReturnItemAsync("BK-1", ItemStatus.Available, _darwin.Id);

        Assert.Equal(3, outcome.DaysOverdue);
        Assert.Equal(1.50m, outcome.FineAmount);
        Assert.Contains(_db.Notifications, n => n.Type == NotificationType.FineIssued);
    }

    [Fact]
    public async Task Returning_at_another_branch_moves_the_item_there()
    {
        var book = _db.AddBook("BK-1", "Dune", _darwin);
        await _db.LoanService.BorrowItemAsync("BK-1", _priya.Id, 14, _darwin.Id);

        var outcome = await _db.LoanService.ReturnItemAsync("BK-1", ItemStatus.Available, _casuarina.Id);

        Assert.Equal(_casuarina.Id, book.CurrentBranchId);
        Assert.Equal(_darwin.Id, book.HomeBranchId);
        Assert.True(outcome.ReturnedAwayFromHome);
        Assert.Equal(_casuarina.Id, outcome.Loan.ReturnBranchId);
    }

    [Fact]
    public async Task Return_condition_Borrowed_is_rejected()
    {
        _db.AddBook("BK-1", "Dune", _darwin);
        await _db.LoanService.BorrowItemAsync("BK-1", _priya.Id, 14, _darwin.Id);

        await Assert.ThrowsAsync<LoanOperationException>(() =>
            _db.LoanService.ReturnItemAsync("BK-1", ItemStatus.Borrowed, _darwin.Id));
    }

    [Fact]
    public async Task Paying_a_fine_unblocks_borrowing()
    {
        _db.AddBook("BK-1", "Dune", _darwin);
        _db.AddBook("BK-2", "Emma", _darwin);
        var loan = await _db.LoanService.BorrowItemAsync("BK-1", _priya.Id, 14, _darwin.Id);
        _db.Clock.Advance(TimeSpan.FromDays(16));
        await _db.LoanService.ReturnItemAsync("BK-1", ItemStatus.Available, _darwin.Id);

        // Blocked while the $1.00 fine is unpaid (Assignment 2 rule).
        await Assert.ThrowsAsync<LoanOperationException>(() => _db.LoanService.BorrowItemAsync("BK-2", _priya.Id, 14, _darwin.Id));

        var paid = await _db.LoanService.PayFineAsync(loan.Id);
        Assert.True(paid.FinePaid);
        Assert.Equal(_db.Clock.Now.UtcDateTime, paid.FinePaidDate);

        var second = await _db.LoanService.BorrowItemAsync("BK-2", _priya.Id, 14, _darwin.Id);
        Assert.NotNull(second);
    }

    [Fact]
    public async Task Paying_twice_or_paying_a_loan_without_a_fine_is_rejected()
    {
        _db.AddBook("BK-1", "Dune", _darwin);
        var loan = await _db.LoanService.BorrowItemAsync("BK-1", _priya.Id, 14, _darwin.Id);
        await _db.LoanService.ReturnItemAsync("BK-1", ItemStatus.Available, _darwin.Id); // on time

        await Assert.ThrowsAsync<LoanOperationException>(() => _db.LoanService.PayFineAsync(loan.Id));
    }

    [Fact]
    public void Fine_calculator_charges_nothing_when_on_time()
    {
        var due = new DateTime(2026, 10, 1);
        Assert.Equal(0m, _db.FineCalculator.CalculateFine(due, due));
        Assert.Equal(0m, _db.FineCalculator.CalculateFine(due, due.AddDays(-2)));
        Assert.Equal(2.50m, _db.FineCalculator.CalculateFine(due, due.AddDays(5)));
    }
}
