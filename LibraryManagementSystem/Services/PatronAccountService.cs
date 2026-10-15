using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;

namespace LibraryManagementSystem.Services;

/// <summary>Everything a patron sees on the kiosk "My account" screen.</summary>
public class PatronAccountSummary
{
    public Borrower Borrower { get; set; } = null!;
    public List<AccountLoanLine> CurrentLoans { get; set; } = new();
    public List<AccountLoanLine> UnpaidFines { get; set; } = new();
    public List<AccountReservationLine> Reservations { get; set; } = new();

    /// <summary>Fines already issued on returned items and not yet paid.</summary>
    public decimal OutstandingFines { get; set; }

    /// <summary>Fines building up on items that are still out and overdue (charged when returned).</summary>
    public decimal AccruingFines { get; set; }

    public bool CanBorrow => OutstandingFines <= 0;
}

public class AccountLoanLine
{
    public int LoanId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string LibraryCode { get; set; } = string.Empty;
    public DateTime BorrowedDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ReturnedDate { get; set; }
    public int DaysOverdue { get; set; }
    public decimal Fine { get; set; }
}

public class AccountReservationLine
{
    public int ReservationId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public ReservationStatus Status { get; set; }
    public string PickupBranch { get; set; } = string.Empty;
    public int QueuePosition { get; set; }
    public DateTime? HoldExpiresAt { get; set; }
}

public interface IPatronAccountService
{
    Task<PatronAccountSummary?> BuildAsync(int borrowerId);
}

public class PatronAccountService : IPatronAccountService
{
    private readonly IBorrowerRepository _borrowers;
    private readonly IReservationService _reservations;
    private readonly IFineCalculator _fineCalculator;
    private readonly TimeProvider _time;

    public PatronAccountService(
        IBorrowerRepository borrowers,
        IReservationService reservations,
        IFineCalculator fineCalculator,
        TimeProvider time)
    {
        _borrowers = borrowers;
        _reservations = reservations;
        _fineCalculator = fineCalculator;
        _time = time;
    }

    public async Task<PatronAccountSummary?> BuildAsync(int borrowerId)
    {
        var borrower = await _borrowers.GetByIdAsync(borrowerId);
        if (borrower is null)
        {
            return null;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var summary = new PatronAccountSummary { Borrower = borrower, OutstandingFines = borrower.OutstandingFines };

        foreach (var loan in borrower.Loans.Where(l => !l.IsReturned).OrderBy(l => l.DueDate))
        {
            var line = ToLine(loan);
            line.DaysOverdue = _fineCalculator.DaysOverdue(loan.DueDate, now);
            line.Fine = _fineCalculator.CalculateFine(loan.DueDate, now);
            summary.CurrentLoans.Add(line);
        }
        summary.AccruingFines = summary.CurrentLoans.Sum(l => l.Fine);

        foreach (var loan in borrower.Loans.Where(l => l.IsReturned && !l.FinePaid && (l.FineAmount ?? 0) > 0))
        {
            var line = ToLine(loan);
            line.Fine = loan.FineAmount ?? 0;
            line.DaysOverdue = loan.DaysOverdue(now);
            summary.UnpaidFines.Add(line);
        }

        foreach (var reservation in borrower.Reservations.Where(r => r.IsActive).OrderBy(r => r.CreatedAt))
        {
            summary.Reservations.Add(new AccountReservationLine
            {
                ReservationId = reservation.Id,
                ItemName = reservation.Item?.Name ?? "Item",
                Status = reservation.Status,
                PickupBranch = reservation.PickupBranch?.Name ?? string.Empty,
                QueuePosition = await _reservations.GetQueuePositionAsync(reservation),
                HoldExpiresAt = reservation.HoldExpiresAt
            });
        }

        return summary;
    }

    private static AccountLoanLine ToLine(Loan loan) => new()
    {
        LoanId = loan.Id,
        ItemName = loan.Item?.Name ?? "Item",
        LibraryCode = loan.Item?.LibraryCode ?? string.Empty,
        BorrowedDate = loan.BorrowedDate,
        DueDate = loan.DueDate,
        ReturnedDate = loan.ReturnedDate
    };
}
