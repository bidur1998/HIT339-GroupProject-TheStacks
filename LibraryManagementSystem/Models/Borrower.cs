using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models;

/// <summary>
/// A member of the public who borrows items. Borrowers are plain data
/// records maintained by reception (requirement 2c) - they are NOT
/// ApplicationUser / Identity accounts, since members of the public never
/// log in to this system; they only appear at the desk or use the public
/// search portal anonymously.
/// </summary>
public class Borrower
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Please enter the borrower's full name.")]
    [StringLength(150)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter a contact email.")]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [StringLength(30)]
    [Display(Name = "Phone Number")]
    public string? PhoneNumber { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    public DateTime DateRegistered { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The number printed (as a barcode) on the patron's library card. The
    /// self-service kiosk and the online hold form identify patrons by this
    /// number plus their PIN. Generated automatically by reception.
    /// </summary>
    [StringLength(20)]
    [Display(Name = "Library card")]
    public string? CardNumber { get; set; }

    /// <summary>
    /// The kiosk PIN, hashed with ASP.NET Core Identity's PasswordHasher -
    /// the plain PIN is never stored.
    /// </summary>
    public string? PinHash { get; set; }

    /// <summary>Failed kiosk/online PIN attempts since the last success (lockout protection).</summary>
    public int FailedPinAttempts { get; set; }

    public DateTime? PinLockedUntil { get; set; }

    [Display(Name = "Home branch")]
    public int? HomeBranchId { get; set; }
    public Branch? HomeBranch { get; set; }

    [Display(Name = "Notify by")]
    public NotificationPreference NotificationPreference { get; set; } = NotificationPreference.Email;

    public List<Loan> Loans { get; set; } = new();

    public List<Reservation> Reservations { get; set; } = new();

    [Display(Name = "Outstanding Fines")]
    public decimal OutstandingFines => Loans
        .Where(l => !l.FinePaid)
        .Sum(l => l.FineAmount ?? 0m);
}
