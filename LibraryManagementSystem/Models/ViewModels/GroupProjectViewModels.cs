using System.ComponentModel.DataAnnotations;
using LibraryManagementSystem.Services;
using LibraryManagementSystem.Services.Reports;

namespace LibraryManagementSystem.Models.ViewModels;

// View models for the Group Project screens. Forms bind to these rather
// than to the EF entities, so a posted form can only change the fields it
// is meant to (no over-posting of things like PinHash or CardNumber).

/// <summary>Reception's add/edit borrower form.</summary>
public class BorrowerFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Please enter the borrower's full name.")]
    [StringLength(150)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter a contact email.")]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [StringLength(30)]
    [Display(Name = "Phone number (for SMS)")]
    public string? PhoneNumber { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    [Display(Name = "Home branch")]
    public int? HomeBranchId { get; set; }

    [Display(Name = "Send notifications by")]
    public NotificationPreference NotificationPreference { get; set; } = NotificationPreference.Email;

    /// <summary>Shown read-only; generated automatically for new borrowers.</summary>
    public string? CardNumber { get; set; }

    public bool HasPin { get; set; }

    [RegularExpression(@"^\d{4,6}$", ErrorMessage = "The PIN must be 4 to 6 digits.")]
    [DataType(DataType.Password)]
    [Display(Name = "Kiosk PIN (4-6 digits)")]
    public string? NewPin { get; set; }

    public static BorrowerFormViewModel FromBorrower(Borrower b) => new()
    {
        Id = b.Id,
        FullName = b.FullName,
        Email = b.Email,
        PhoneNumber = b.PhoneNumber,
        Address = b.Address,
        HomeBranchId = b.HomeBranchId,
        NotificationPreference = b.NotificationPreference,
        CardNumber = b.CardNumber,
        HasPin = !string.IsNullOrEmpty(b.PinHash)
    };

    public void CopyTo(Borrower b)
    {
        b.FullName = FullName.Trim();
        b.Email = Email.Trim();
        b.PhoneNumber = string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim();
        b.Address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim();
        b.HomeBranchId = HomeBranchId;
        b.NotificationPreference = NotificationPreference;
    }
}

/// <summary>Public "place a hold" form - patron identifies with card + PIN.</summary>
public class PlaceHoldViewModel
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ItemStatus { get; set; } = string.Empty;
    public int QueueLength { get; set; }

    [Required(ErrorMessage = "Enter your library card number.")]
    [StringLength(20)]
    [Display(Name = "Library card number")]
    public string CardNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your PIN.")]
    [DataType(DataType.Password)]
    [StringLength(6, MinimumLength = 4)]
    public string Pin { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose where you'd like to collect it.")]
    [Display(Name = "Pick up at")]
    public int PickupBranchId { get; set; }
}

public class HoldConfirmationViewModel
{
    public string ItemName { get; set; } = string.Empty;
    public string BorrowerName { get; set; } = string.Empty;
    public string PickupBranch { get; set; } = string.Empty;
    public int QueuePosition { get; set; }
}

/// <summary>Staff "request a transfer" form.</summary>
public class TransferRequestViewModel
{
    [Required(ErrorMessage = "Enter the item's library code.")]
    [Display(Name = "Library code")]
    public string LibraryCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose the destination branch.")]
    [Display(Name = "Send to branch")]
    public int ToBranchId { get; set; }

    [StringLength(200)]
    public string? Reason { get; set; }
}

public class NotificationsIndexViewModel
{
    public NotificationType? Type { get; set; }
    public NotificationChannel? Channel { get; set; }
    public string? Query { get; set; }
    public int TotalCount { get; set; }
    public List<NotificationLog> Logs { get; set; } = new();
}

public class ReportsViewModel
{
    public ReportType ReportType { get; set; } = ReportType.Borrowing;
    public int? BranchId { get; set; }

    [DataType(DataType.Date)]
    public DateTime? From { get; set; }

    [DataType(DataType.Date)]
    public DateTime? To { get; set; }

    public Report? Report { get; set; }
    public List<Branch> Branches { get; set; } = new();
}

public class ImportIndexViewModel
{
    public ImportPreview? Preview { get; set; }
    public string? ExternalQuery { get; set; }
    public List<ExternalMediaRecord> ExternalResults { get; set; } = new();
    public string ProviderName { get; set; } = string.Empty;
    public List<Branch> Branches { get; set; } = new();
    public string DefaultBranchCode { get; set; } = string.Empty;
}

/// <summary>Everything the reception "Holds" screen shows.</summary>
public class HoldShelfViewModel
{
    public Branch? DeskBranch { get; set; }
    public List<Reservation> ReadyHere { get; set; } = new();
    public List<Reservation> AwaitingTransfer { get; set; } = new();
    public List<Reservation> Waiting { get; set; } = new();
}

public class TransfersIndexViewModel
{
    public Branch? DeskBranch { get; set; }
    public List<BranchTransfer> ToDispatch { get; set; } = new();
    public List<BranchTransfer> ToReceive { get; set; } = new();
    public List<BranchTransfer> OtherOpen { get; set; } = new();
    public List<BranchTransfer> RecentlyClosed { get; set; } = new();
    public List<Branch> Branches { get; set; } = new();
    public TransferRequestViewModel NewRequest { get; set; } = new();
}

// ---------------------------------------------------------------------
// Kiosk
// ---------------------------------------------------------------------

public class KioskWelcomeViewModel
{
    public Branch? Branch { get; set; }
    public bool IsOpenNow { get; set; }

    [Display(Name = "Library card number")]
    public string? CardNumber { get; set; }
}

public class KioskPinViewModel
{
    public string MaskedCard { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your PIN.")]
    public string Pin { get; set; } = string.Empty;
}

public class KioskCheckoutViewModel
{
    public string BorrowerName { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;

    [Display(Name = "Item library code")]
    public string? LibraryCode { get; set; }

    /// <summary>Items borrowed during this kiosk visit, newest first.</summary>
    public List<KioskReceiptLine> Receipt { get; set; } = new();
}

public class KioskReceiptLine
{
    public string ItemName { get; set; } = string.Empty;
    public string LibraryCode { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
}
