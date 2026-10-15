using LibraryManagementSystem.Models;
using LibraryManagementSystem.Services;
using LibraryManagementSystem.Tests.TestSupport;

namespace LibraryManagementSystem.Tests;

/// <summary>Holds, queue order, notifying the next patron, transfers for pickup, expiry.</summary>
public class ReservationServiceTests
{
    private readonly InMemoryLibrary _db = new();
    private readonly Branch _darwin;
    private readonly Branch _palmerston;
    private readonly Borrower _priya;
    private readonly Borrower _sam;
    private readonly Borrower _lena;

    public ReservationServiceTests()
    {
        _darwin = _db.AddBranch("DAR", "Darwin City");
        _palmerston = _db.AddBranch("PAL", "Palmerston");
        _priya = _db.AddBorrower("Priya Nair");
        _sam = _db.AddBorrower("Sam Okafor");
        _lena = _db.AddBorrower("Lena Fischer", NotificationPreference.Sms, phone: "0400 555 666");
    }

    [Fact]
    public async Task Cannot_reserve_an_item_that_is_on_the_shelf()
    {
        var book = _db.AddBook("BK-1", "Dune", _darwin);

        await Assert.ThrowsAsync<ReservationException>(() =>
            _db.ReservationService.PlaceHoldAsync(book.Id, _priya.Id, _darwin.Id));
    }

    [Fact]
    public async Task Can_reserve_a_damaged_item_and_patron_is_told_their_queue_position()
    {
        var book = _db.AddBook("BK-1", "Dune", _darwin, ItemStatus.Damaged);

        var reservation = await _db.ReservationService.PlaceHoldAsync(book.Id, _priya.Id, _darwin.Id);

        Assert.Equal(ReservationStatus.Waiting, reservation.Status);
        Assert.Equal(1, await _db.ReservationService.GetQueuePositionAsync(reservation));
        Assert.Contains(_db.Notifications, n => n.Type == NotificationType.ReservationPlaced && n.BorrowerId == _priya.Id);
    }

    [Fact]
    public async Task Cannot_reserve_a_destroyed_item_or_join_the_same_queue_twice()
    {
        var destroyed = _db.AddBook("BK-1", "Dune", _darwin, ItemStatus.Destroyed);
        var damaged = _db.AddBook("BK-2", "Emma", _darwin, ItemStatus.Damaged);

        await Assert.ThrowsAsync<ReservationException>(() => _db.ReservationService.PlaceHoldAsync(destroyed.Id, _priya.Id, _darwin.Id));

        await _db.ReservationService.PlaceHoldAsync(damaged.Id, _priya.Id, _darwin.Id);
        await Assert.ThrowsAsync<ReservationException>(() => _db.ReservationService.PlaceHoldAsync(damaged.Id, _priya.Id, _darwin.Id));
    }

    [Fact]
    public async Task Return_makes_item_ready_for_FIRST_patron_in_queue_and_notifies_them()
    {
        _db.AddBook("BK-1", "Dune", _darwin);
        await _db.LoanService.BorrowItemAsync("BK-1", _sam.Id, 14, _darwin.Id);
        var item = _db.Items.Single();

        var first = await _db.ReservationService.PlaceHoldAsync(item.Id, _priya.Id, _darwin.Id);
        _db.Clock.Advance(TimeSpan.FromMinutes(5));
        var second = await _db.ReservationService.PlaceHoldAsync(item.Id, _lena.Id, _darwin.Id);

        var outcome = await _db.LoanService.ReturnItemAsync("BK-1", ItemStatus.Available, _darwin.Id);

        Assert.Equal(first.Id, outcome.NextReservation?.Id);
        Assert.Equal(ReservationStatus.ReadyForPickup, first.Status);
        Assert.Equal(_db.Clock.Now.UtcDateTime.AddDays(3), first.HoldExpiresAt);
        Assert.Equal(ReservationStatus.Waiting, second.Status);
        Assert.Contains(_db.Notifications, n => n.Type == NotificationType.ReservationReady && n.BorrowerId == _priya.Id);
        Assert.DoesNotContain(_db.Notifications, n => n.Type == NotificationType.ReservationReady && n.BorrowerId == _lena.Id);
    }

    [Fact]
    public async Task Nobody_else_can_borrow_an_item_held_for_someone_but_the_holder_can()
    {
        _db.AddBook("BK-1", "Dune", _darwin, ItemStatus.Damaged);
        var item = _db.Items.Single();
        var hold = await _db.ReservationService.PlaceHoldAsync(item.Id, _priya.Id, _darwin.Id);

        // Admin repairs it -> ready for Priya.
        item.Status = ItemStatus.Available;
        await _db.ReservationService.OnItemBecameAvailableAsync(item);

        await Assert.ThrowsAsync<ReservationException>(() => _db.LoanService.BorrowItemAsync("BK-1", _sam.Id, 14, _darwin.Id));

        await _db.LoanService.BorrowItemAsync("BK-1", _priya.Id, 14, _darwin.Id);
        Assert.Equal(ReservationStatus.Fulfilled, hold.Status);
    }

    [Fact]
    public async Task Item_at_wrong_branch_is_transferred_and_patron_notified_only_when_it_arrives()
    {
        _db.AddBook("BK-1", "Dune", _darwin);
        var item = _db.Items.Single();
        await _db.LoanService.BorrowItemAsync("BK-1", _sam.Id, 14, _darwin.Id);
        var hold = await _db.ReservationService.PlaceHoldAsync(item.Id, _lena.Id, _palmerston.Id);

        await _db.LoanService.ReturnItemAsync("BK-1", ItemStatus.Available, _darwin.Id);

        Assert.Equal(ReservationStatus.AwaitingTransfer, hold.Status);
        var transfer = Assert.Single(_db.Transfers);
        Assert.Equal(_darwin.Id, transfer.FromBranchId);
        Assert.Equal(_palmerston.Id, transfer.ToBranchId);
        Assert.Equal(hold.Id, transfer.ReservationId);
        Assert.DoesNotContain(_db.Notifications, n => n.Type == NotificationType.ReservationReady);

        await _db.TransferService.DispatchAsync(transfer.Id, "darwin staff", _darwin.Id);
        await _db.TransferService.ReceiveAsync(transfer.Id, "palmerston staff", _palmerston.Id);

        Assert.Equal(_palmerston.Id, item.CurrentBranchId);
        Assert.Equal(ReservationStatus.ReadyForPickup, hold.Status);
        // Lena prefers SMS, so the "ready" message goes by SMS.
        Assert.Contains(_db.Notifications, n => n.Type == NotificationType.ReservationReady && n.Channel == NotificationChannel.Sms);
    }

    [Fact]
    public async Task Uncollected_hold_expires_and_passes_to_next_patron()
    {
        _db.AddBook("BK-1", "Dune", _darwin, ItemStatus.Damaged);
        var item = _db.Items.Single();
        var first = await _db.ReservationService.PlaceHoldAsync(item.Id, _priya.Id, _darwin.Id);
        _db.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = await _db.ReservationService.PlaceHoldAsync(item.Id, _sam.Id, _darwin.Id);

        item.Status = ItemStatus.Available;
        await _db.ReservationService.OnItemBecameAvailableAsync(item);
        Assert.Equal(ReservationStatus.ReadyForPickup, first.Status);

        _db.Clock.Advance(TimeSpan.FromDays(4)); // past the 3-day pickup window
        int expired = await _db.ReservationService.ExpireUncollectedHoldsAsync();

        Assert.Equal(1, expired);
        Assert.Equal(ReservationStatus.Expired, first.Status);
        Assert.Equal(ReservationStatus.ReadyForPickup, second.Status);
    }

    [Fact]
    public async Task Destroying_an_item_cancels_its_waitlist()
    {
        _db.AddBook("BK-1", "Dune", _darwin);
        var item = _db.Items.Single();
        await _db.LoanService.BorrowItemAsync("BK-1", _sam.Id, 14, _darwin.Id);
        var hold = await _db.ReservationService.PlaceHoldAsync(item.Id, _priya.Id, _darwin.Id);

        await _db.LoanService.ReturnItemAsync("BK-1", ItemStatus.Destroyed, _darwin.Id);

        Assert.Equal(ReservationStatus.Cancelled, hold.Status);
        Assert.Contains(_db.Notifications, n => n.Type == NotificationType.ReservationCancelled && n.BorrowerId == _priya.Id);
    }

    [Fact]
    public async Task Patron_can_only_cancel_their_own_hold()
    {
        _db.AddBook("BK-1", "Dune", _darwin, ItemStatus.Damaged);
        var item = _db.Items.Single();
        var hold = await _db.ReservationService.PlaceHoldAsync(item.Id, _priya.Id, _darwin.Id);

        await Assert.ThrowsAsync<ReservationException>(() => _db.ReservationService.CancelAsync(hold.Id, _sam.Id, "test"));

        await _db.ReservationService.CancelAsync(hold.Id, _priya.Id, "test");
        Assert.Equal(ReservationStatus.Cancelled, hold.Status);
    }
}
