using LibraryManagementSystem.Models;
using LibraryManagementSystem.Models.ViewModels;
using LibraryManagementSystem.Repositories;
using LibraryManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LibraryManagementSystem.Controllers;

/// <summary>
/// Digital Reservation &amp; Waitlist Queue (Group Project).
/// Public side: a patron places a hold from the search page using their
/// library card + PIN. Staff side: reception sees the hold shelf for their
/// branch and every waitlist, and can cancel a hold.
/// </summary>
public class ReservationsController : Controller
{
    private readonly IReservationService _reservationService;
    private readonly IReservationRepository _reservations;
    private readonly IItemRepository _items;
    private readonly IBranchRepository _branches;
    private readonly IPatronAuthService _patronAuth;
    private readonly IBranchContext _branchContext;

    public ReservationsController(
        IReservationService reservationService,
        IReservationRepository reservations,
        IItemRepository items,
        IBranchRepository branches,
        IPatronAuthService patronAuth,
        IBranchContext branchContext)
    {
        _reservationService = reservationService;
        _reservations = reservations;
        _items = items;
        _branches = branches;
        _patronAuth = patronAuth;
        _branchContext = branchContext;
    }

    // ------------------------------------------------------------------
    // Public: place a hold
    // ------------------------------------------------------------------

    [AllowAnonymous]
    public async Task<IActionResult> Place(int id)
    {
        var item = await _items.GetByIdAsync(id);
        if (item is null) return NotFound();

        var model = new PlaceHoldViewModel
        {
            ItemId = item.Id,
            ItemName = item.Name,
            ItemStatus = item.Status.ToString(),
            QueueLength = (await _reservations.GetActiveQueueForItemAsync(id)).Count,
            PickupBranchId = item.CurrentBranchId ?? item.HomeBranchId ?? 0
        };

        ViewBag.Branches = await _branches.GetAllAsync();
        return View(model);
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("patron-pin")]
    public async Task<IActionResult> Place(PlaceHoldViewModel model)
    {
        var item = await _items.GetByIdAsync(model.ItemId);
        if (item is null) return NotFound();

        model.ItemName = item.Name;
        model.ItemStatus = item.Status.ToString();
        model.QueueLength = (await _reservations.GetActiveQueueForItemAsync(item.Id)).Count;

        if (ModelState.IsValid)
        {
            var auth = await _patronAuth.VerifyAsync(model.CardNumber, model.Pin);
            if (!auth.Succeeded)
            {
                ModelState.AddModelError(string.Empty, auth.Message);
            }
            else
            {
                try
                {
                    var reservation = await _reservationService.PlaceHoldAsync(item.Id, auth.Borrower!.Id, model.PickupBranchId);
                    var pickup = await _branches.GetByIdAsync(reservation.PickupBranchId);
                    return View("Confirmation", new HoldConfirmationViewModel
                    {
                        ItemName = item.Name,
                        BorrowerName = auth.Borrower.FullName,
                        PickupBranch = pickup?.Name ?? string.Empty,
                        QueuePosition = await _reservationService.GetQueuePositionAsync(reservation)
                    });
                }
                catch (BusinessRuleException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
            }
        }

        // Never send the PIN back to the browser.
        model.Pin = string.Empty;
        ViewBag.Branches = await _branches.GetAllAsync();
        return View(model);
    }

    // ------------------------------------------------------------------
    // Staff: hold shelf and waitlists
    // ------------------------------------------------------------------

    [Authorize(Roles = Roles.Reception + "," + Roles.Admin)]
    public async Task<IActionResult> Index()
    {
        var desk = await _branchContext.GetCurrentBranchAsync();
        var active = await _reservations.GetAllActiveAsync();

        var model = new HoldShelfViewModel
        {
            DeskBranch = desk,
            ReadyHere = active.Where(r => r.Status == ReservationStatus.ReadyForPickup && r.PickupBranchId == desk?.Id).OrderBy(r => r.HoldExpiresAt).ToList(),
            AwaitingTransfer = active.Where(r => r.Status == ReservationStatus.AwaitingTransfer).ToList(),
            Waiting = active.Where(r => r.Status == ReservationStatus.Waiting
                                        || (r.Status == ReservationStatus.ReadyForPickup && r.PickupBranchId != desk?.Id))
                            .OrderBy(r => r.Item.Name).ThenBy(r => r.CreatedAt).ToList()
        };

        return View(model);
    }

    [Authorize(Roles = Roles.Reception + "," + Roles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        try
        {
            var reservation = await _reservationService.CancelAsync(id, borrowerId: null, reason: "cancelled by library staff.");
            TempData["StatusMessage"] = $"Hold on '{reservation.Item.Name}' for {reservation.Borrower.FullName} was cancelled.";
        }
        catch (BusinessRuleException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
