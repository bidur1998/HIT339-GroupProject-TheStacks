namespace LibraryManagementSystem.Models;

/// <summary>How a borrower would like to be contacted.</summary>
public enum NotificationPreference
{
    Email,
    Sms,
    EmailAndSms
}

public enum NotificationChannel
{
    Email,
    Sms
}

public enum NotificationType
{
    ItemBorrowed,
    DueSoon,
    OverdueFineAccruing,
    FineIssued,
    FinePaid,
    ReservationPlaced,
    ReservationReady,
    ReservationCancelled
}

public enum DeliveryStatus
{
    /// <summary>The message was "sent" by the simulator (written to this log and the console).</summary>
    Simulated,
    /// <summary>Not sent - e.g. SMS requested but the borrower has no phone number.</summary>
    Skipped
}

/// <summary>
/// One simulated email or SMS (Group Project - Automated Multi-Channel
/// Notification System). No real messages leave the system: each "send" is
/// written here and to the console log, and staff review them on the
/// Notifications dashboard. A real email/SMS provider could replace the
/// simulated senders later without changing anything that calls them.
/// </summary>
public class NotificationLog
{
    public int Id { get; set; }

    public int? BorrowerId { get; set; }
    public Borrower? Borrower { get; set; }

    public int? LoanId { get; set; }
    public int? ReservationId { get; set; }

    public NotificationType Type { get; set; }
    public NotificationChannel Channel { get; set; }

    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DeliveryStatus DeliveryStatus { get; set; } = DeliveryStatus.Simulated;
    public string? StatusDetail { get; set; }
}
