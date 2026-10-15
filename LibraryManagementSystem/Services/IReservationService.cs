using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Services;

public class ReservationException : BusinessRuleException
{
    public ReservationException(string message) : base(message) { }
}

/// <summary>
/// Digital Reservation &amp; Waitlist Queue (Group Project). Holds the rules
/// for who can reserve what, the first-come-first-served queue, and what
/// happens when an item becomes available again. LoanService and
/// ItemsController call the "On..." methods whenever an item's status or
/// location changes, so the queue reacts no matter which screen caused it.
/// </summary>
public interface IReservationService
{
    /// <summary>Places a hold on an item that is Borrowed, Damaged or already held for someone else.</summary>
    Task<Reservation> PlaceHoldAsync(int itemId, int borrowerId, int pickupBranchId);

    /// <summary>
    /// Cancels an active reservation. Pass <paramref name="borrowerId"/> when
    /// the patron cancels it themselves, so they can only cancel their own.
    /// </summary>
    Task<Reservation> CancelAsync(int reservationId, int? borrowerId, string reason);

    /// <summary>
    /// Call when an item's status changes to Available (returned in good
    /// condition, or repaired by an admin). Moves the next patron in the
    /// queue to Ready (and notifies them), or starts a transfer if the item
    /// is at the wrong branch. Returns the reservation that was advanced, if any.
    /// </summary>
    Task<Reservation?> OnItemBecameAvailableAsync(Item item);

    /// <summary>Call after a transfer is received - completes holds that were waiting for the item to arrive.</summary>
    Task<Reservation?> OnItemArrivedAtBranchAsync(Item item);

    /// <summary>Call when an item is Destroyed - cancels its waitlist and tells everyone in it.</summary>
    Task<int> OnItemWithdrawnAsync(Item item, string reason);

    /// <summary>
    /// Throws if the item has a waitlist and this borrower isn't first in it,
    /// so nobody can jump the queue at the desk or kiosk.
    /// </summary>
    Task EnsureBorrowerMayTakeAsync(Item item, int borrowerId);

    /// <summary>Closes the borrower's reservation once they actually borrow the item.</summary>
    Task MarkFulfilledAsync(int itemId, int borrowerId);

    /// <summary>Expires holds that weren't collected in time and passes the item on. Returns how many expired.</summary>
    Task<int> ExpireUncollectedHoldsAsync();

    /// <summary>1-based place in the queue, or 0 if the reservation isn't active.</summary>
    Task<int> GetQueuePositionAsync(Reservation reservation);
}
