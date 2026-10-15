namespace LibraryManagementSystem.Models;

/// <summary>
/// One borrow/return transaction: which item, which borrower, when it was
/// borrowed, when it is due, when (if ever) it came back, and any fine
/// that resulted. This is the record that both the reception "borrow /
/// return" workflow and the manager statistics screens are built on.
/// </summary>
public class Loan
{
    public int Id { get; set; }

    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public int BorrowerId { get; set; }
    public Borrower Borrower { get; set; } = null!;

    public DateTime BorrowedDate { get; set; } = DateTime.UtcNow;

    public DateTime DueDate { get; set; }

    /// <summary>Null while the item is still out.</summary>
    public DateTime? ReturnedDate { get; set; }

    /// <summary>Null until the item is returned and a fine is calculated.</summary>
    public decimal? FineAmount { get; set; }

    public bool FinePaid { get; set; }

    /// <summary>When reception recorded the fine as paid (used by the fine revenue audit).</summary>
    public DateTime? FinePaidDate { get; set; }

    /// <summary>The branch whose desk or kiosk lent the item out.</summary>
    public int? BorrowBranchId { get; set; }
    public Branch? BorrowBranch { get; set; }

    /// <summary>The branch where the item was handed back (may differ from where it was borrowed).</summary>
    public int? ReturnBranchId { get; set; }
    public Branch? ReturnBranch { get; set; }

    /// <summary>"Desk" or "Kiosk" - lets the manager see how much the kiosk is used.</summary>
    public string? BorrowChannel { get; set; }

    /// <summary>The condition the item was recorded in when it was returned.</summary>
    public ItemStatus? ConditionOnReturn { get; set; }

    public bool IsReturned => ReturnedDate.HasValue;

    public bool IsOverdue => !IsReturned && DateTime.UtcNow.Date > DueDate.Date;

    public int DaysOverdue(DateTime asOf) =>
        Math.Max(0, ((ReturnedDate ?? asOf).Date - DueDate.Date).Days);
}
