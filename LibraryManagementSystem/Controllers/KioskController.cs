using LibraryManagementSystem.Models;
using LibraryManagementSystem.Models.ViewModels;
using LibraryManagementSystem.Repositories;
using LibraryManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace LibraryManagementSystem.Controllers;

/// <summary>
/// Public Self-Service Kiosk (Group Project). A separate, simplified,
/// touch-friendly route (/Kiosk) with its own layout for in-library kiosk
/// stations: scan a library card, enter a PIN, then scan items to check
/// them out or view the account summary.
///
/// The kiosk is a shared public device, so the patron "session" is kept
/// short: it lives in the server-side session, ends after
/// KioskSettings:IdleTimeoutSeconds without activity, and is cleared when
/// the patron taps Finish. Barcode scanners type the code and press Enter,
/// so a plain auto-focused text box works as the "scan" input.
/// </summary>
[AllowAnonymous]
public class KioskController : Controller
{
    private const string BranchKey = "Kiosk_BranchCode";
    private const string PendingCardKey = "Kiosk_PendingCard";
    private const string BorrowerKey = "Kiosk_BorrowerId";
    private const string ExpiresKey = "Kiosk_ExpiresUtcTicks";
    private const string ReceiptKey = "Kiosk_Receipt";

    private readonly IPatronAuthService _patronAuth;
    private readonly IPatronAccountService _accounts;
    private readonly ILoanService _loanService;
    private readonly IReservationService _reservations;
    private readonly IBranchRepository _branches;
    private readonly IBranchContext _branchContext;
    private readonly TimeProvider _time;
    private readonly KioskSettings _kioskSettings;
    private readonly LibrarySettings _librarySettings;

    public KioskController(
        IPatronAuthService patronAuth,
        IPatronAccountService accounts,
        ILoanService loanService,
        IReservationService reservations,
        IBranchRepository branches,
        IBranchContext branchContext,
        TimeProvider time,
        IOptions<KioskSettings> kioskSettings,
        IOptions<LibrarySettings> librarySettings)
    {
        _patronAuth = patronAuth;
        _accounts = accounts;
        _loanService = loanService;
        _reservations = reservations;
        _branches = branches;
        _branchContext = branchContext;
        _time = time;
        _kioskSettings = kioskSettings.Value;
        _librarySettings = librarySettings.Value;
    }

    // ------------------------------------------------------------------
    // Step 1: welcome / scan card
    // ------------------------------------------------------------------

    /// <param name="branch">Optional - lets one machine act as the kiosk for a given branch, e.g. /Kiosk?branch=CAS.</param>
    public async Task<IActionResult> Index(string? branch)
    {
        if (!string.IsNullOrWhiteSpace(branch) && await _branches.GetByCodeAsync(branch) is not null)
        {
            HttpContext.Session.SetString(BranchKey, branch.Trim().ToUpperInvariant());
        }

        EndPatronSession();
        var kioskBranch = await GetKioskBranchAsync();
        PrepareLayout(kioskBranch);

        return View(new KioskWelcomeViewModel
        {
            Branch = kioskBranch,
            IsOpenNow = kioskBranch?.IsOpenAt(_branchContext.LocalNow) ?? false
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Card(string? cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
        {
            TempData["KioskError"] = "Please scan or type your library card number.";
            return RedirectToAction(nameof(Index));
        }

        // Always go to the PIN screen, whether or not the card exists, so the
        // kiosk can't be used to discover valid card numbers.
        HttpContext.Session.SetString(PendingCardKey, cardNumber.Trim().ToUpperInvariant());
        return RedirectToAction(nameof(Pin));
    }

    // ------------------------------------------------------------------
    // Step 2: PIN
    // ------------------------------------------------------------------

    public async Task<IActionResult> Pin()
    {
        var card = HttpContext.Session.GetString(PendingCardKey);
        if (card is null)
        {
            return RedirectToAction(nameof(Index));
        }

        PrepareLayout(await GetKioskBranchAsync());
        return View(new KioskPinViewModel { MaskedCard = Mask(card) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("patron-pin")]
    public async Task<IActionResult> Pin(KioskPinViewModel model)
    {
        var card = HttpContext.Session.GetString(PendingCardKey);
        if (card is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var result = await _patronAuth.VerifyAsync(card, model.Pin ?? string.Empty);
        if (!result.Succeeded)
        {
            TempData["KioskError"] = result.Message;
            if (result.Status == PatronAuthStatus.LockedOut || result.Status == PatronAuthStatus.NoPinSet)
            {
                HttpContext.Session.Remove(PendingCardKey);
                return RedirectToAction(nameof(Index));
            }
            return RedirectToAction(nameof(Pin));
        }

        HttpContext.Session.Remove(PendingCardKey);
        HttpContext.Session.SetInt32(BorrowerKey, result.Borrower!.Id);
        HttpContext.Session.Remove(ReceiptKey);
        Touch();
        return RedirectToAction(nameof(Menu));
    }

    // ------------------------------------------------------------------
    // Step 3: menu, checkout, account
    // ------------------------------------------------------------------

    public async Task<IActionResult> Menu()
    {
        var borrowerId = ActiveBorrowerId();
        if (borrowerId is null) return SessionEnded();

        var summary = await _accounts.BuildAsync(borrowerId.Value);
        if (summary is null) return SessionEnded();

        PrepareLayout(await GetKioskBranchAsync());
        return View(summary);
    }

    public async Task<IActionResult> Checkout()
    {
        var borrowerId = ActiveBorrowerId();
        if (borrowerId is null) return SessionEnded();

        return View(await BuildCheckoutModelAsync(borrowerId.Value));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(string? libraryCode)
    {
        var borrowerId = ActiveBorrowerId();
        if (borrowerId is null) return SessionEnded();

        if (string.IsNullOrWhiteSpace(libraryCode))
        {
            TempData["KioskError"] = "Scan the barcode on the item, or type its library code.";
            return RedirectToAction(nameof(Checkout));
        }

        try
        {
            var branch = await GetKioskBranchAsync();
            var loan = await _loanService.BorrowItemAsync(
                libraryCode, borrowerId.Value, _librarySettings.DefaultLoanPeriodDays, branch?.Id, "Kiosk");

            AddToReceipt(new KioskReceiptLine { ItemName = loan.Item.Name, LibraryCode = loan.Item.LibraryCode, DueDate = loan.DueDate });
            TempData["KioskSuccess"] = $"\"{loan.Item.Name}\" is checked out to you. Due back {loan.DueDate:dddd d MMMM}.";
        }
        catch (BusinessRuleException ex)
        {
            TempData["KioskError"] = ex.Message;
        }

        return RedirectToAction(nameof(Checkout));
    }

    public async Task<IActionResult> Account()
    {
        var borrowerId = ActiveBorrowerId();
        if (borrowerId is null) return SessionEnded();

        var summary = await _accounts.BuildAsync(borrowerId.Value);
        if (summary is null) return SessionEnded();

        PrepareLayout(await GetKioskBranchAsync());
        return View(summary);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelHold(int reservationId)
    {
        var borrowerId = ActiveBorrowerId();
        if (borrowerId is null) return SessionEnded();

        try
        {
            // Passing the borrower id means a patron can only cancel their own hold.
            await _reservations.CancelAsync(reservationId, borrowerId.Value, "you cancelled it at the kiosk.");
            TempData["KioskSuccess"] = "Your hold was cancelled.";
        }
        catch (BusinessRuleException ex)
        {
            TempData["KioskError"] = ex.Message;
        }

        return RedirectToAction(nameof(Account));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Finish()
    {
        EndPatronSession();
        TempData["KioskSuccess"] = "You're signed out. Thanks for visiting The Stacks!";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Called by kiosk.js when nobody has touched the screen for a while.</summary>
    public IActionResult Timeout()
    {
        EndPatronSession();
        TempData["KioskError"] = "Your session ended because the kiosk wasn't used for a while.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<KioskCheckoutViewModel> BuildCheckoutModelAsync(int borrowerId)
    {
        var summary = await _accounts.BuildAsync(borrowerId);
        var branch = await GetKioskBranchAsync();
        PrepareLayout(branch);

        return new KioskCheckoutViewModel
        {
            BorrowerName = summary?.Borrower.FullName ?? string.Empty,
            BranchName = branch?.Name ?? string.Empty,
            Receipt = ReadReceipt()
        };
    }

    private async Task<Branch?> GetKioskBranchAsync()
    {
        var code = HttpContext.Session.GetString(BranchKey);
        return (code is null ? null : await _branches.GetByCodeAsync(code))
               ?? await _branchContext.GetCurrentBranchAsync();
    }

    /// <summary>Returns the signed-in kiosk patron, or null if there isn't one or it has timed out.</summary>
    private int? ActiveBorrowerId()
    {
        var id = HttpContext.Session.GetInt32(BorrowerKey);
        var expiresText = HttpContext.Session.GetString(ExpiresKey);
        if (id is null || expiresText is null || !long.TryParse(expiresText, out long ticks))
        {
            return null;
        }

        if (_time.GetUtcNow().UtcDateTime.Ticks > ticks)
        {
            EndPatronSession();
            return null;
        }

        Touch();
        return id;
    }

    private void Touch()
    {
        var expires = _time.GetUtcNow().UtcDateTime.AddSeconds(_kioskSettings.IdleTimeoutSeconds);
        HttpContext.Session.SetString(ExpiresKey, expires.Ticks.ToString());
    }

    private void EndPatronSession()
    {
        HttpContext.Session.Remove(BorrowerKey);
        HttpContext.Session.Remove(ExpiresKey);
        HttpContext.Session.Remove(PendingCardKey);
        HttpContext.Session.Remove(ReceiptKey);
    }

    private IActionResult SessionEnded()
    {
        TempData["KioskError"] = "Please scan your card to start.";
        return RedirectToAction(nameof(Index));
    }

    private void PrepareLayout(Branch? branch)
    {
        ViewData["KioskBranchName"] = branch?.Name ?? "The Stacks";
        ViewData["KioskIdleSeconds"] = _kioskSettings.IdleTimeoutSeconds;
        ViewData["KioskSignedIn"] = HttpContext.Session.GetInt32(BorrowerKey).HasValue;
    }

    private List<KioskReceiptLine> ReadReceipt()
    {
        var json = HttpContext.Session.GetString(ReceiptKey);
        return json is null
            ? new List<KioskReceiptLine>()
            : System.Text.Json.JsonSerializer.Deserialize<List<KioskReceiptLine>>(json) ?? new List<KioskReceiptLine>();
    }

    private void AddToReceipt(KioskReceiptLine line)
    {
        var receipt = ReadReceipt();
        receipt.Insert(0, line);
        HttpContext.Session.SetString(ReceiptKey, System.Text.Json.JsonSerializer.Serialize(receipt));
    }

    private static string Mask(string card) =>
        card.Length <= 3 ? card : new string('*', card.Length - 3) + card[^3..];
}
