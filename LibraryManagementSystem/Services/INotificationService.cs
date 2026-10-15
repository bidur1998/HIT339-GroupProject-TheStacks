using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Services;

/// <summary>The content of one message, before it is sent on any channel.</summary>
/// <param name="EmailBody">Full text for email.</param>
/// <param name="SmsText">Short text for SMS (kept under 160 characters where possible).</param>
public record NotificationMessage(NotificationType Type, string Subject, string EmailBody, string SmsText);

/// <summary>Result of trying to deliver on one channel.</summary>
public record ChannelSendResult(bool Delivered, string? Detail);

/// <summary>
/// One delivery channel (email or SMS). The app only ships simulated
/// senders, but because NotificationService depends on this interface a
/// real provider (e.g. an SMTP or SMS gateway client) could be registered
/// in Program.cs instead, without changing any calling code (DIP again).
/// </summary>
public interface INotificationChannelSender
{
    NotificationChannel Channel { get; }

    Task<ChannelSendResult> SendAsync(string recipient, string subject, string body);
}

/// <summary>
/// Sends a message to a borrower on whichever channels they prefer, and
/// writes every attempt to the NotificationLog table (Group Project -
/// Automated Multi-Channel Notification System).
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Never throws because of a delivery problem - a failed "send" must not
    /// stop a book being borrowed or returned. Problems are logged as Skipped.
    /// </summary>
    Task<List<NotificationLog>> SendAsync(Borrower borrower, NotificationMessage message, int? loanId = null, int? reservationId = null);
}

/// <summary>Result of one run of the scheduled reminder check.</summary>
public class ReminderRunResult
{
    public int DueSoonSent { get; set; }
    public int OverdueSent { get; set; }
    public int HoldsExpired { get; set; }

    public override string ToString() =>
        $"{DueSoonSent} due-soon reminder(s), {OverdueSent} overdue/fine notice(s), {HoldsExpired} expired hold(s).";
}

/// <summary>
/// The "automated" part of notifications: finds loans that are nearly due
/// or overdue and sends reminders, and expires uncollected holds. Runs on a
/// timer in the background (ReminderBackgroundService) and can also be run
/// on demand by staff from the Notifications dashboard for testing.
/// </summary>
public interface IReminderService
{
    Task<ReminderRunResult> RunAsync();
}
