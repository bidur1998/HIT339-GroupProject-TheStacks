namespace LibraryManagementSystem.Models;

public enum TransferStatus
{
    Requested,
    InTransit,
    Received,
    Cancelled
}

/// <summary>
/// Moving a physical item from one branch to another (Group Project -
/// Multi-Branch Inventory). Staff at the sending branch dispatch it, staff
/// at the receiving branch mark it received, and only then does the item's
/// CurrentBranch change. While a transfer is open the item can't be
/// borrowed, so the catalogue never claims an item is on a shelf it isn't on.
/// </summary>
public class BranchTransfer
{
    public int Id { get; set; }

    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public int FromBranchId { get; set; }
    public Branch FromBranch { get; set; } = null!;

    public int ToBranchId { get; set; }
    public Branch ToBranch { get; set; } = null!;

    public TransferStatus Status { get; set; } = TransferStatus.Requested;

    public string? Reason { get; set; }

    /// <summary>Set when the transfer was created automatically to fill a reservation.</summary>
    public int? ReservationId { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string RequestedBy { get; set; } = string.Empty;

    public DateTime? DispatchedAt { get; set; }
    public string? DispatchedBy { get; set; }

    public DateTime? ReceivedAt { get; set; }
    public string? ReceivedBy { get; set; }

    public DateTime? CancelledAt { get; set; }

    public bool IsOpen => Status is TransferStatus.Requested or TransferStatus.InTransit;
}
