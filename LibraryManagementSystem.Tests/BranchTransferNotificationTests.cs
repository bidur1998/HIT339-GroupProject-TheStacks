using LibraryManagementSystem.Models;
using LibraryManagementSystem.Services;
using LibraryManagementSystem.Tests.TestSupport;

namespace LibraryManagementSystem.Tests;

/// <summary>Transfers, branch opening hours, reminders and patron PIN checks.</summary>
public class BranchTransferNotificationTests
{
    private readonly InMemoryLibrary _db = new();
    private readonly Branch _darwin;
    private readonly Branch _casuarina;
    private readonly Borrower _priya;

    public BranchTransferNotificationTests()
    {
        _darwin = _db.AddBranch("DAR", "Darwin City");
        _casuarina = _db.AddBranch("CAS", "Casuarina");
        _priya = _db.AddBorrower("Priya Nair", NotificationPreference.EmailAndSms, phone: "0400 111 222");
    }

    // ---------------- Transfers ----------------

    [Fact]
    public async Task Transfer_moves_item_only_when_received()
    {
        var book = _db.AddBook("BK-1", "Dune", _darwin);

        var transfer = await _db.TransferService.RequestAsync(book.Id, _casuarina.Id, "robin", "display");
        Assert.Equal(_darwin.Id, book.CurrentBranchId);

        await _db.TransferService.DispatchAsync(transfer.Id, "robin", _darwin.Id);
        Assert.Equal(TransferStatus.InTransit, transfer.Status);
        Assert.Equal(_darwin.Id, book.CurrentBranchId);

        await _db.TransferService.ReceiveAsync(transfer.Id, "casey", _casuarina.Id);
        Assert.Equal(TransferStatus.Received, transfer.Status);
        Assert.Equal(_casuarina.Id, book.CurrentBranchId);
        Assert.Equal(_darwin.Id, book.HomeBranchId);
    }

    [Fact]
    public async Task Transfer_rules_are_enforced()
    {
        var onShelf = _db.AddBook("BK-1", "Dune", _darwin);
        var onLoan = _db.AddBook("BK-2", "Emma", _darwin, ItemStatus.Borrowed);

        await Assert.ThrowsAsync<TransferException>(() => _db.TransferService.RequestAsync(onShelf.Id, _darwin.Id, "x", null)); // same branch
        await Assert.ThrowsAsync<TransferException>(() => _db.TransferService.RequestAsync(onLoan.Id, _casuarina.Id, "x", null)); // on loan

        var t = await _db.TransferService.RequestAsync(onShelf.Id, _casuarina.Id, "x", null);
        await Assert.ThrowsAsync<TransferException>(() => _db.TransferService.RequestAsync(onShelf.Id, _casuarina.Id, "x", null)); // already open

        // Only the sending branch can dispatch, only the receiving branch can receive.
        await Assert.ThrowsAsync<TransferException>(() => _db.TransferService.DispatchAsync(t.Id, "x", _casuarina.Id));
        await _db.TransferService.DispatchAsync(t.Id, "x", _darwin.Id);
        await Assert.ThrowsAsync<TransferException>(() => _db.TransferService.ReceiveAsync(t.Id, "x", _darwin.Id));
    }

    // ---------------- Branch hours ----------------

    [Fact]
    public void Branch_open_status_follows_hours_weekends_and_closures()
    {
        var branch = new Branch { OpeningTime = new TimeOnly(9, 0), ClosingTime = new TimeOnly(17, 0), OpenOnWeekends = false };
        var wednesdayNoon = new DateTime(2026, 10, 14, 12, 0, 0);
        var wednesdayEvening = new DateTime(2026, 10, 14, 17, 30, 0);
        var saturdayNoon = new DateTime(2026, 10, 17, 12, 0, 0);

        Assert.True(branch.IsOpenAt(wednesdayNoon));
        Assert.False(branch.IsOpenAt(wednesdayEvening));
        Assert.False(branch.IsOpenAt(saturdayNoon));

        branch.OperatingStatus = BranchOperatingStatus.TemporarilyClosed;
        Assert.False(branch.IsOpenAt(wednesdayNoon));
    }

    // ---------------- Notifications ----------------

    [Fact]
    public async Task Email_and_sms_preference_logs_one_message_per_channel()
    {
        var logs = await _db.NotificationService.SendAsync(_priya,
            new NotificationMessage(NotificationType.FinePaid, "Subject", "Email body", "SMS text"));

        Assert.Equal(2, logs.Count);
        Assert.Contains(logs, l => l.Channel == NotificationChannel.Email && l.Recipient == _priya.Email && l.DeliveryStatus == DeliveryStatus.Simulated);
        Assert.Contains(logs, l => l.Channel == NotificationChannel.Sms && l.Body == "SMS text");
    }

    [Fact]
    public async Task Sms_without_a_phone_number_is_logged_as_skipped_not_thrown()
    {
        var noPhone = _db.AddBorrower("Tom Walker", NotificationPreference.Sms);

        var logs = await _db.NotificationService.SendAsync(noPhone,
            new NotificationMessage(NotificationType.DueSoon, "Subject", "Body", "Text"));

        var log = Assert.Single(logs);
        Assert.Equal(DeliveryStatus.Skipped, log.DeliveryStatus);
    }

    [Fact]
    public async Task Reminder_job_sends_due_soon_once_and_overdue_once_per_day()
    {
        _db.AddBook("BK-1", "Dune", _darwin);
        _db.AddBook("BK-2", "Emma", _darwin);
        await _db.LoanService.BorrowItemAsync("BK-1", _priya.Id, 2, _darwin.Id);   // due in 2 days
        await _db.LoanService.BorrowItemAsync("BK-2", _priya.Id, 30, _darwin.Id);  // not due for ages

        var first = await _db.ReminderService.RunAsync();
        var again = await _db.ReminderService.RunAsync();
        Assert.Equal(1, first.DueSoonSent);
        Assert.Equal(0, again.DueSoonSent); // no duplicate reminder

        _db.Clock.Advance(TimeSpan.FromDays(5)); // BK-1 now 3 days overdue
        var overdue = await _db.ReminderService.RunAsync();
        var sameDay = await _db.ReminderService.RunAsync();
        Assert.Equal(1, overdue.OverdueSent);
        Assert.Equal(0, sameDay.OverdueSent);

        var notice = _db.Notifications.Last(n => n.Type == NotificationType.OverdueFineAccruing);
        Assert.Contains("$1.50", notice.Subject);

        _db.Clock.Advance(TimeSpan.FromDays(1));
        var nextDay = await _db.ReminderService.RunAsync();
        Assert.Equal(1, nextDay.OverdueSent);
    }

    // ---------------- Patron PIN ----------------

    [Fact]
    public async Task Pin_check_succeeds_then_locks_card_after_too_many_wrong_attempts()
    {
        _db.PatronAuth.SetPin(_priya, "1234");
        Assert.NotEqual("1234", _priya.PinHash); // stored hashed, never plain

        Assert.True((await _db.PatronAuth.VerifyAsync(_priya.CardNumber!, "1234")).Succeeded);

        for (int i = 0; i < 2; i++)
        {
            Assert.Equal(PatronAuthStatus.InvalidCredentials, (await _db.PatronAuth.VerifyAsync(_priya.CardNumber!, "0000")).Status);
        }
        Assert.Equal(PatronAuthStatus.LockedOut, (await _db.PatronAuth.VerifyAsync(_priya.CardNumber!, "0000")).Status);

        // Even the right PIN is refused while locked...
        Assert.Equal(PatronAuthStatus.LockedOut, (await _db.PatronAuth.VerifyAsync(_priya.CardNumber!, "1234")).Status);

        // ...until the lockout period is over.
        _db.Clock.Advance(TimeSpan.FromMinutes(16));
        Assert.True((await _db.PatronAuth.VerifyAsync(_priya.CardNumber!, "1234")).Succeeded);
    }

    [Fact]
    public async Task Unknown_card_and_wrong_pin_give_the_same_message()
    {
        _db.PatronAuth.SetPin(_priya, "1234");

        var unknown = await _db.PatronAuth.VerifyAsync("C9999999", "1234");
        var wrong = await _db.PatronAuth.VerifyAsync(_priya.CardNumber!, "9999");

        Assert.Equal(unknown.Message, wrong.Message);
    }

    [Fact]
    public void Pin_must_be_4_to_6_digits()
    {
        Assert.Throws<BusinessRuleException>(() => _db.PatronAuth.SetPin(_priya, "12"));
        Assert.Throws<BusinessRuleException>(() => _db.PatronAuth.SetPin(_priya, "abcd"));
        _db.PatronAuth.SetPin(_priya, "123456");
    }

    [Fact]
    public async Task Account_summary_shows_overdue_loans_accruing_fines_and_queue_position()
    {
        _db.AddBook("BK-1", "Dune", _darwin);
        var damaged = _db.AddBook("BK-2", "Emma", _darwin, ItemStatus.Damaged);
        await _db.LoanService.BorrowItemAsync("BK-1", _priya.Id, 14, _darwin.Id);
        await _db.ReservationService.PlaceHoldAsync(damaged.Id, _priya.Id, _darwin.Id);
        _db.Clock.Advance(TimeSpan.FromDays(18)); // 4 days overdue

        var summary = await _db.PatronAccounts.BuildAsync(_priya.Id);

        Assert.NotNull(summary);
        var loan = Assert.Single(summary!.CurrentLoans);
        Assert.Equal(4, loan.DaysOverdue);
        Assert.Equal(2.00m, summary.AccruingFines);
        Assert.Equal(1, Assert.Single(summary.Reservations).QueuePosition);
    }
}
