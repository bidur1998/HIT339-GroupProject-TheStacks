using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;

namespace LibraryManagementSystem.Services;

public class LoanService : ILoanService
{
    private readonly IItemRepository _items;
    private readonly IBorrowerRepository _borrowers;
    private readonly ILoanRepository _loans;
    private readonly IFineCalculator _fineCalculator;

    // Group Project additions: loans now interact with branches, the
    // reservation queue, transfers and notifications.
    private readonly IReservationService _reservations;
    private readonly ITransferRepository _transfers;
    private readonly INotificationService _notifications;
    private readonly TimeProvider _time;

    public LoanService(
        IItemRepository items,
        IBorrowerRepository borrowers,
        ILoanRepository loans,
        IFineCalculator fineCalculator,
        IReservationService reservations,
        ITransferRepository transfers,
        INotificationService notifications,
        TimeProvider time)
    {
        _items = items;
        _borrowers = borrowers;
        _loans = loans;
        _fineCalculator = fineCalculator;
        _reservations = reservations;
        _transfers = transfers;
        _notifications = notifications;
        _time = time;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public async Task<Loan> BorrowItemAsync(string libraryCode, int borrowerId, int loanPeriodDays, int? branchId = null, string channel = "Desk")
    {
        if (loanPeriodDays < 1 || loanPeriodDays > 90)
        {
            throw new LoanOperationException("The loan period must be between 1 and 90 days.");
        }

        var item = await _items.GetByLibraryCodeAsync(libraryCode)
            ?? throw new LoanOperationException($"No item found with library code '{libraryCode}'.");

        if (item.Status != ItemStatus.Available)
        {
            throw new LoanOperationException(
                $"'{item.Name}' is not available to borrow (current status: {item.Status}).");
        }

        // Multi-branch rule: a desk/kiosk can only lend what is physically on its own shelves.
        if (branchId.HasValue && item.CurrentBranchId.HasValue && item.CurrentBranchId != branchId)
        {
            throw new LoanOperationException(
                $"'{item.Name}' is shelved at {item.CurrentBranch?.Name ?? "another branch"}, not this branch. " +
                "Staff can request a transfer if the patron would like it here.");
        }

        if (await _transfers.GetOpenForItemAsync(item.Id) is not null)
        {
            throw new LoanOperationException($"'{item.Name}' is being transferred between branches and can't be borrowed yet.");
        }

        var borrower = await _borrowers.GetByIdAsync(borrowerId)
            ?? throw new LoanOperationException("Selected borrower could not be found.");

        if (borrower.OutstandingFines > 0)
        {
            throw new LoanOperationException(
                $"{borrower.FullName} has ${borrower.OutstandingFines:0.00} in unpaid fines. " +
                "Outstanding fines must be paid before another item can be borrowed.");
        }

        // Reservation rule: nobody can jump the waitlist.
        await _reservations.EnsureBorrowerMayTakeAsync(item, borrower.Id);

        var now = Now;
        var loan = new Loan
        {
            ItemId = item.Id,
            BorrowerId = borrower.Id,
            BorrowedDate = now,
            DueDate = now.AddDays(loanPeriodDays),
            BorrowBranchId = branchId ?? item.CurrentBranchId,
            BorrowChannel = channel
        };

        await _loans.AddAsync(loan);

        item.Status = ItemStatus.Borrowed;
        await _items.UpdateAsync(item);

        // If this borrower had reserved the item, their hold is now fulfilled.
        await _reservations.MarkFulfilledAsync(item.Id, borrower.Id);

        loan.Item = item;
        loan.Borrower = borrower;
        await _notifications.SendAsync(borrower,
            NotificationTemplates.ItemBorrowed(loan, item.Name, item.CurrentBranch?.Name),
            loanId: loan.Id);

        return loan;
    }

    public async Task<ReturnOutcome> ReturnItemAsync(string libraryCode, ItemStatus conditionOnReturn, int? branchId = null)
    {
        // "Borrowed" isn't a condition - guards against a tampered form.
        if (conditionOnReturn == ItemStatus.Borrowed)
        {
            throw new LoanOperationException("Please record the item's condition as good, damaged or destroyed.");
        }

        var item = await _items.GetByLibraryCodeAsync(libraryCode)
            ?? throw new LoanOperationException($"No item found with library code '{libraryCode}'.");

        var loan = await _loans.GetActiveLoanForItemAsync(item.Id)
            ?? throw new LoanOperationException($"'{item.Name}' does not appear to be currently on loan.");

        var now = Now;
        loan.ReturnedDate = now;
        loan.ConditionOnReturn = conditionOnReturn;
        loan.ReturnBranchId = branchId ?? item.CurrentBranchId;

        var daysOverdue = _fineCalculator.DaysOverdue(loan.DueDate, now);
        var fine = _fineCalculator.CalculateFine(loan.DueDate, now);
        loan.FineAmount = fine > 0 ? fine : null;

        await _loans.UpdateAsync(loan);

        // The item's new status reflects the condition reception recorded
        // on return - Available, Damaged or Destroyed (requirement 1c).
        item.Status = conditionOnReturn;

        // Multi-branch: the item is now physically at the branch it was returned to.
        if (branchId.HasValue)
        {
            item.CurrentBranchId = branchId;
        }
        await _items.UpdateAsync(item);

        var outcome = new ReturnOutcome
        {
            Loan = loan,
            DaysOverdue = daysOverdue,
            FineAmount = fine,
            ReturnedAwayFromHome = item.HomeBranchId.HasValue && item.CurrentBranchId != item.HomeBranchId
        };

        if (fine > 0)
        {
            await _notifications.SendAsync(loan.Borrower,
                NotificationTemplates.FineIssued(loan, item.Name, fine, daysOverdue),
                loanId: loan.Id);
        }

        // Reservation queue reacts to the new status.
        if (conditionOnReturn == ItemStatus.Available)
        {
            outcome.NextReservation = await _reservations.OnItemBecameAvailableAsync(item);
        }
        else if (conditionOnReturn == ItemStatus.Destroyed)
        {
            await _reservations.OnItemWithdrawnAsync(item, "the item was damaged beyond repair and has been withdrawn.");
        }

        return outcome;
    }

    public async Task<Loan> PayFineAsync(int loanId)
    {
        var loan = await _loans.GetByIdAsync(loanId)
            ?? throw new LoanOperationException("That loan could not be found.");

        if (loan.FineAmount is null or <= 0)
        {
            throw new LoanOperationException("This loan has no fine to pay.");
        }

        if (loan.FinePaid)
        {
            throw new LoanOperationException("This fine has already been paid.");
        }

        loan.FinePaid = true;
        loan.FinePaidDate = Now;
        await _loans.UpdateAsync(loan);

        await _notifications.SendAsync(loan.Borrower,
            NotificationTemplates.FinePaid(loan, loan.Item.Name, loan.FineAmount.Value),
            loanId: loan.Id);

        return loan;
    }
}
