using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Services;

/// <summary>
/// Base type for any library rule being broken (Group Project). Controllers
/// catch this one type and show the message to the user, so every module
/// (loans, reservations, transfers, kiosk...) reports problems the same way.
/// </summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message) { }
}

/// <summary>Thrown for any borrow/return rule violation, caught by the controller and shown to the user.</summary>
public class LoanOperationException : BusinessRuleException
{
    public LoanOperationException(string message) : base(message) { }
}

public class ReturnOutcome
{
    public Loan Loan { get; set; } = null!;
    public int DaysOverdue { get; set; }
    public decimal FineAmount { get; set; }

    // --- Group Project additions ---

    /// <summary>The next patron in the waitlist, if the return released the item to them.</summary>
    public Reservation? NextReservation { get; set; }

    /// <summary>True when the item was handed back at a branch other than its home branch.</summary>
    public bool ReturnedAwayFromHome { get; set; }
}

/// <summary>
/// The high-level policy for borrowing and returning items (requirement
/// 2a/2b). ReceptionController depends only on this interface, never on
/// the repositories or the fine calculator directly - those are wired in
/// here via constructor injection, keeping the controller thin and the
/// business rules in one testable place.
/// </summary>
public interface ILoanService
{
    /// <param name="branchId">The branch whose desk/kiosk is lending the item (Group Project). Null = don't check location.</param>
    /// <param name="channel">"Desk" or "Kiosk".</param>
    Task<Loan> BorrowItemAsync(string libraryCode, int borrowerId, int loanPeriodDays, int? branchId = null, string channel = "Desk");

    /// <param name="branchId">The branch the item is being handed back at (Group Project).</param>
    Task<ReturnOutcome> ReturnItemAsync(string libraryCode, ItemStatus conditionOnReturn, int? branchId = null);

    /// <summary>Records that the fine on a loan has been paid (fixes the Assignment 2 gap noted in my report).</summary>
    Task<Loan> PayFineAsync(int loanId);
}
