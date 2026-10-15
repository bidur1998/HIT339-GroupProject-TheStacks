using LibraryManagementSystem.Models;
using LibraryManagementSystem.Models.ViewModels;
using LibraryManagementSystem.Repositories;
using LibraryManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagementSystem.Controllers;

/// <summary>
/// Reception's borrow/return counter (requirements 2a and 2b). Depends
/// only on ILoanService and IBorrowerRepository - never touches EF Core or
/// even knows a database is involved.
///
/// Group Project: the desk now belongs to a branch (IBranchContext), so
/// it can only lend items on its own shelves, and returns update where the
/// item physically is and trigger the reservation queue.
/// </summary>
[Authorize(Roles = Roles.Reception)]
public class ReceptionController : Controller
{
    private const string LastCodeSessionKey = "Reception_LastLibraryCode";

    private readonly ILoanService _loanService;
    private readonly IBorrowerRepository _borrowers;
    private readonly IBranchContext _branchContext;

    public ReceptionController(ILoanService loanService, IBorrowerRepository borrowers, IBranchContext branchContext)
    {
        _loanService = loanService;
        _borrowers = borrowers;
        _branchContext = branchContext;
    }

    public async Task<IActionResult> Borrow()
    {
        await PopulateBorrowersAsync();

        var model = new BorrowViewModel();

        // Session is read here, in the controller, as a small convenience
        // - it pre-fills the last code reception typed so scanning several
        // items for the same borrower is quicker. Per the unit material,
        // session values are only ever touched from controllers, and only
        // ever pushed to the view via ViewBag/ViewData, never read directly
        // in Razor.
        var lastCode = HttpContext.Session.GetString(LastCodeSessionKey);
        if (!string.IsNullOrEmpty(lastCode))
        {
            ViewBag.LastLibraryCode = lastCode;
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Borrow(BorrowViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateBorrowersAsync();
            return View(model);
        }

        try
        {
            var desk = await _branchContext.GetCurrentBranchAsync();
            var loan = await _loanService.BorrowItemAsync(model.LibraryCode, model.BorrowerId, model.LoanPeriodDays, desk?.Id, "Desk");
            HttpContext.Session.SetString(LastCodeSessionKey, model.LibraryCode);

            TempData["StatusMessage"] =
                $"'{loan.Item.Name}' was borrowed by {loan.Borrower.FullName}, due back {loan.DueDate:d MMM yyyy}. A confirmation was sent to the borrower.";
            return RedirectToAction(nameof(Borrow));
        }
        catch (BusinessRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateBorrowersAsync();
            return View(model);
        }
    }

    public IActionResult Return() => View(new ReturnViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Return(ReturnViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var desk = await _branchContext.GetCurrentBranchAsync();
            var outcome = await _loanService.ReturnItemAsync(model.LibraryCode, model.ConditionOnReturn, desk?.Id);

            var resultVm = new ReturnResultViewModel
            {
                ItemName = outcome.Loan.Item.Name,
                BorrowerName = outcome.Loan.Borrower.FullName,
                DaysOverdue = outcome.DaysOverdue,
                FineAmount = outcome.FineAmount
            };

            // Tell the desk what to do with the item next (hold shelf,
            // transfer tote, or back on the shelf).
            ViewBag.NextStep = DescribeNextStep(outcome, desk);

            return View("ReturnResult", resultVm);
        }
        catch (BusinessRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    private static string? DescribeNextStep(ReturnOutcome outcome, Branch? desk)
    {
        var next = outcome.NextReservation;
        if (next is not null)
        {
            return next.Status == ReservationStatus.ReadyForPickup
                ? $"This item is reserved. Put it on the hold shelf for {next.Borrower.FullName} - they have been notified."
                : $"This item is reserved for pickup at another branch. A transfer request was created - put it in the transfer tote.";
        }

        if (outcome.ReturnedAwayFromHome)
        {
            return $"This item belongs to another branch but has been returned to {desk?.Name ?? "this branch"}. It can stay here, or request a transfer home from the Transfers page.";
        }

        return null;
    }

    private async Task PopulateBorrowersAsync()
    {
        ViewBag.Borrowers = await _borrowers.GetAllAsync();
    }
}
