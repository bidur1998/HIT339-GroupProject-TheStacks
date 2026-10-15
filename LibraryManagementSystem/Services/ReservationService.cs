using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;
using Microsoft.Extensions.Options;

namespace LibraryManagementSystem.Services;

public class ReservationService : IReservationService
{
    private readonly IReservationRepository _reservations;
    private readonly IItemRepository _items;
    private readonly IBorrowerRepository _borrowers;
    private readonly IBranchRepository _branches;
    private readonly ITransferRepository _transfers;
    private readonly INotificationService _notifications;
    private readonly TimeProvider _time;
    private readonly ReservationSettings _settings;

    public ReservationService(
        IReservationRepository reservations,
        IItemRepository items,
        IBorrowerRepository borrowers,
        IBranchRepository branches,
        ITransferRepository transfers,
        INotificationService notifications,
        TimeProvider time,
        IOptions<ReservationSettings> settings)
    {
        _reservations = reservations;
        _items = items;
        _borrowers = borrowers;
        _branches = branches;
        _transfers = transfers;
        _notifications = notifications;
        _time = time;
        _settings = settings.Value;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public async Task<Reservation> PlaceHoldAsync(int itemId, int borrowerId, int pickupBranchId)
    {
        var item = await _items.GetByIdAsync(itemId)
            ?? throw new ReservationException("That item could not be found.");

        if (item.Status == ItemStatus.Destroyed)
        {
            throw new ReservationException($"'{item.Name}' has been withdrawn from the collection and can't be reserved.");
        }

        var queue = await _reservations.GetActiveQueueForItemAsync(itemId);

        // The brief's rule: holds are for items that are Borrowed or Damaged.
        // An Available item with nobody waiting is on the shelf - just borrow it.
        if (item.Status == ItemStatus.Available && !queue.Any())
        {
            string where = item.CurrentBranch?.Name ?? "the library";
            throw new ReservationException($"'{item.Name}' is on the shelf at {where} right now, so there's no need to reserve it - you can borrow it straight away.");
        }

        var borrower = await _borrowers.GetByIdAsync(borrowerId)
            ?? throw new ReservationException("That library card could not be found.");

        if (borrower.Loans.Any(l => l.ItemId == itemId && !l.IsReturned))
        {
            throw new ReservationException($"You already have '{item.Name}' on loan.");
        }

        if (queue.Any(r => r.BorrowerId == borrowerId))
        {
            throw new ReservationException($"You're already on the waitlist for '{item.Name}'.");
        }

        var activeForBorrower = await _reservations.GetActiveForBorrowerAsync(borrowerId);
        if (activeForBorrower.Count >= _settings.MaxActiveHoldsPerBorrower)
        {
            throw new ReservationException($"You can have at most {_settings.MaxActiveHoldsPerBorrower} active holds at a time.");
        }

        var pickupBranch = await _branches.GetByIdAsync(pickupBranchId)
            ?? throw new ReservationException("Please choose a valid pickup branch.");

        var reservation = new Reservation
        {
            ItemId = item.Id,
            BorrowerId = borrower.Id,
            PickupBranchId = pickupBranch.Id,
            CreatedAt = Now,
            Status = ReservationStatus.Waiting
        };
        await _reservations.AddAsync(reservation);

        reservation.Borrower = borrower;
        int position = queue.Count + 1;
        await _notifications.SendAsync(borrower,
            NotificationTemplates.ReservationPlaced(reservation, item.Name, position, pickupBranch.Name),
            reservationId: reservation.Id);

        return reservation;
    }

    public async Task<Reservation> CancelAsync(int reservationId, int? borrowerId, string reason)
    {
        var reservation = await _reservations.GetByIdAsync(reservationId)
            ?? throw new ReservationException("That reservation could not be found.");

        if (borrowerId.HasValue && reservation.BorrowerId != borrowerId.Value)
        {
            throw new ReservationException("You can only cancel your own reservations.");
        }

        if (!reservation.IsActive)
        {
            throw new ReservationException("That reservation is no longer active.");
        }

        bool wasHoldingItem = reservation.IsHoldingItem;

        reservation.Status = ReservationStatus.Cancelled;
        reservation.ClosedAt = Now;
        await _reservations.UpdateAsync(reservation);

        await _notifications.SendAsync(reservation.Borrower,
            NotificationTemplates.ReservationCancelled(reservation, reservation.Item.Name, reason),
            reservationId: reservation.Id);

        // If this patron's copy was on the hold shelf, offer it to the next person.
        if (wasHoldingItem)
        {
            await OnItemBecameAvailableAsync(reservation.Item);
        }

        return reservation;
    }

    public async Task<Reservation?> OnItemBecameAvailableAsync(Item item)
    {
        if (item.Status != ItemStatus.Available)
        {
            return null;
        }

        var queue = await _reservations.GetActiveQueueForItemAsync(item.Id);

        // Someone is already being held this copy - nothing to do.
        if (queue.Any(r => r.IsHoldingItem))
        {
            return null;
        }

        var next = queue.FirstOrDefault(r => r.Status == ReservationStatus.Waiting);
        if (next is null)
        {
            return null;
        }

        bool atPickupBranch = item.CurrentBranchId is null || item.CurrentBranchId == next.PickupBranchId;
        if (atPickupBranch)
        {
            await MarkReadyAndNotifyAsync(next, item);
            return next;
        }

        // Item is at a different branch: hold it for this patron and ask
        // staff to send it to their pickup branch. They're notified when it
        // arrives (OnItemArrivedAtBranchAsync).
        next.Status = ReservationStatus.AwaitingTransfer;
        await _reservations.UpdateAsync(next);

        var openTransfer = await _transfers.GetOpenForItemAsync(item.Id);
        if (openTransfer is null)
        {
            await _transfers.AddAsync(new BranchTransfer
            {
                ItemId = item.Id,
                FromBranchId = item.CurrentBranchId!.Value,
                ToBranchId = next.PickupBranchId,
                Status = TransferStatus.Requested,
                Reason = $"Reservation #{next.Id} - patron collecting at pickup branch",
                ReservationId = next.Id,
                RequestedAt = Now,
                RequestedBy = "System (reservation queue)"
            });
        }

        return next;
    }

    public async Task<Reservation?> OnItemArrivedAtBranchAsync(Item item)
    {
        if (item.Status != ItemStatus.Available)
        {
            return null;
        }

        var queue = await _reservations.GetActiveQueueForItemAsync(item.Id);
        var waiting = queue.FirstOrDefault(r =>
            r.Status == ReservationStatus.AwaitingTransfer && r.PickupBranchId == item.CurrentBranchId);

        if (waiting is null)
        {
            // Not a reservation transfer (or it went to the wrong branch):
            // treat it like any other newly available item.
            return await OnItemBecameAvailableAsync(item);
        }

        await MarkReadyAndNotifyAsync(waiting, item);
        return waiting;
    }

    public async Task<int> OnItemWithdrawnAsync(Item item, string reason)
    {
        var queue = await _reservations.GetActiveQueueForItemAsync(item.Id);
        foreach (var reservation in queue)
        {
            reservation.Status = ReservationStatus.Cancelled;
            reservation.ClosedAt = Now;
            await _reservations.UpdateAsync(reservation);

            await _notifications.SendAsync(reservation.Borrower,
                NotificationTemplates.ReservationCancelled(reservation, item.Name, reason),
                reservationId: reservation.Id);
        }

        return queue.Count;
    }

    public async Task EnsureBorrowerMayTakeAsync(Item item, int borrowerId)
    {
        var queue = await _reservations.GetActiveQueueForItemAsync(item.Id);
        if (!queue.Any())
        {
            return;
        }

        // Whoever is being held the item goes first; otherwise the oldest hold.
        var first = queue.FirstOrDefault(r => r.IsHoldingItem) ?? queue.First();
        if (first.BorrowerId != borrowerId)
        {
            string until = first.HoldExpiresAt.HasValue ? $" (held until {first.HoldExpiresAt:d MMM})" : string.Empty;
            throw new ReservationException(
                $"'{item.Name}' is reserved for another patron{until}. This borrower can join the waitlist instead.");
        }
    }

    public async Task MarkFulfilledAsync(int itemId, int borrowerId)
    {
        var queue = await _reservations.GetActiveQueueForItemAsync(itemId);
        var mine = queue.FirstOrDefault(r => r.BorrowerId == borrowerId);
        if (mine is null)
        {
            return;
        }

        mine.Status = ReservationStatus.Fulfilled;
        mine.ClosedAt = Now;
        await _reservations.UpdateAsync(mine);
    }

    public async Task<int> ExpireUncollectedHoldsAsync()
    {
        var expired = await _reservations.GetExpiredReadyHoldsAsync(Now);
        foreach (var reservation in expired)
        {
            reservation.Status = ReservationStatus.Expired;
            reservation.ClosedAt = Now;
            await _reservations.UpdateAsync(reservation);

            await _notifications.SendAsync(reservation.Borrower,
                NotificationTemplates.ReservationCancelled(reservation, reservation.Item.Name, "it wasn't collected before the pickup deadline."),
                reservationId: reservation.Id);

            // Offer the item to the next person in line.
            await OnItemBecameAvailableAsync(reservation.Item);
        }

        return expired.Count;
    }

    public async Task<int> GetQueuePositionAsync(Reservation reservation)
    {
        if (!reservation.IsActive)
        {
            return 0;
        }

        var queue = await _reservations.GetActiveQueueForItemAsync(reservation.ItemId);
        int index = queue.FindIndex(r => r.Id == reservation.Id);
        return index < 0 ? 0 : index + 1;
    }

    private async Task MarkReadyAndNotifyAsync(Reservation reservation, Item item)
    {
        reservation.Status = ReservationStatus.ReadyForPickup;
        reservation.ReadyAt = Now;
        reservation.HoldExpiresAt = Now.AddDays(_settings.HoldPickupDays);
        await _reservations.UpdateAsync(reservation);

        var branch = await _branches.GetByIdAsync(reservation.PickupBranchId);
        await _notifications.SendAsync(reservation.Borrower,
            NotificationTemplates.ReservationReady(reservation, item.Name, branch?.Name ?? "your pickup branch", reservation.HoldExpiresAt.Value),
            reservationId: reservation.Id);
    }
}
