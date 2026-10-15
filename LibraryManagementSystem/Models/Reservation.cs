namespace LibraryManagementSystem.Models;

/// <summary>Lifecycle of a hold placed by a patron.</summary>
public enum ReservationStatus
{
    /// <summary>In the queue, waiting for the item to come back or be repaired.</summary>
    Waiting,
    /// <summary>Item is available, but it is being moved to the patron's pickup branch first.</summary>
    AwaitingTransfer,
    /// <summary>Item is on the hold shelf at the pickup branch; the patron has been notified.</summary>
    ReadyForPickup,
    Fulfilled,
    Cancelled,
    Expired
}

/// <summary>
/// A hold on an item that is currently Borrowed or Damaged (Group Project -
/// Digital Reservation &amp; Waitlist Queue). Queue order is first come,
/// first served by CreatedAt. When the item becomes Available again, the
/// first Waiting reservation is moved to ReadyForPickup (or AwaitingTransfer
/// if the item is at a different branch) and the patron is notified.
/// </summary>
public class Reservation
{
    public int Id { get; set; }

    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public int BorrowerId { get; set; }
    public Borrower Borrower { get; set; } = null!;

    public int PickupBranchId { get; set; }
    public Branch PickupBranch { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ReservationStatus Status { get; set; } = ReservationStatus.Waiting;

    /// <summary>When the item was put on the hold shelf and the patron notified.</summary>
    public DateTime? ReadyAt { get; set; }

    /// <summary>The patron must collect the item before this time or the hold expires.</summary>
    public DateTime? HoldExpiresAt { get; set; }

    /// <summary>When the reservation was fulfilled, cancelled or expired.</summary>
    public DateTime? ClosedAt { get; set; }

    public bool IsActive => Status is ReservationStatus.Waiting
        or ReservationStatus.AwaitingTransfer
        or ReservationStatus.ReadyForPickup;

    /// <summary>True when the item is being held for this patron (nobody else may borrow it).</summary>
    public bool IsHoldingItem => Status is ReservationStatus.AwaitingTransfer or ReservationStatus.ReadyForPickup;
}
