using LibraryManagementSystem.Models;
using LibraryManagementSystem.Models.ViewModels;
using LibraryManagementSystem.Repositories;
using LibraryManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagementSystem.Controllers;

/// <summary>
/// Reception's borrower CRUD screens (requirement 2c).
///
/// Group Project changes: the forms now bind to BorrowerFormViewModel
/// instead of the Borrower entity (so a form can't overwrite the PIN hash,
/// card number or registration date), new borrowers get a library card
/// number automatically, reception can set the kiosk PIN, and fines can
/// finally be recorded as paid.
/// </summary>
[Authorize(Roles = Roles.Reception)]
public class BorrowersController : Controller
{
    private readonly IBorrowerRepository _borrowers;
    private readonly IBranchRepository _branches;
    private readonly IPatronAuthService _patronAuth;
    private readonly ILoanService _loanService;

    public BorrowersController(
        IBorrowerRepository borrowers,
        IBranchRepository branches,
        IPatronAuthService patronAuth,
        ILoanService loanService)
    {
        _borrowers = borrowers;
        _branches = branches;
        _patronAuth = patronAuth;
        _loanService = loanService;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _borrowers.GetAllAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var borrower = await _borrowers.GetByIdAsync(id);
        if (borrower is null) return NotFound();
        return View(borrower);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateBranchesAsync();
        return View(new BorrowerFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BorrowerFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateBranchesAsync();
            return View(model);
        }

        var borrower = new Borrower();
        model.CopyTo(borrower);
        borrower.CardNumber = await _patronAuth.GenerateCardNumberAsync();
        if (!string.IsNullOrEmpty(model.NewPin))
        {
            _patronAuth.SetPin(borrower, model.NewPin);
        }

        await _borrowers.AddAsync(borrower);
        TempData["StatusMessage"] = $"'{borrower.FullName}' was added as a borrower. Library card number: {borrower.CardNumber}.";
        return RedirectToAction(nameof(Details), new { id = borrower.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var borrower = await _borrowers.GetByIdAsync(id);
        if (borrower is null) return NotFound();
        await PopulateBranchesAsync();
        return View(BorrowerFormViewModel.FromBorrower(borrower));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BorrowerFormViewModel model)
    {
        if (id != model.Id) return BadRequest();

        // Load the stored record and copy only the editable fields onto it,
        // rather than saving whatever the form posted (which in Assignment 2
        // also reset DateRegistered, as it wasn't on the form).
        var borrower = await _borrowers.GetByIdAsync(id);
        if (borrower is null) return NotFound();

        if (!ModelState.IsValid)
        {
            model.CardNumber = borrower.CardNumber;
            model.HasPin = !string.IsNullOrEmpty(borrower.PinHash);
            await PopulateBranchesAsync();
            return View(model);
        }

        model.CopyTo(borrower);
        if (!string.IsNullOrEmpty(model.NewPin))
        {
            _patronAuth.SetPin(borrower, model.NewPin);
        }

        await _borrowers.UpdateAsync(borrower);
        TempData["StatusMessage"] = $"'{borrower.FullName}' was updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var borrower = await _borrowers.GetByIdAsync(id);
        if (borrower is null) return NotFound();
        return View(borrower);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var borrower = await _borrowers.GetByIdAsync(id);
        if (borrower is null) return NotFound();

        if (borrower.Loans.Any(l => !l.IsReturned))
        {
            TempData["ErrorMessage"] = $"'{borrower.FullName}' currently has items on loan and cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        // Loans, fines and reservations are kept for the audit trail, so a
        // borrower with history can't be hard-deleted (the database would
        // refuse anyway because of the Restrict foreign keys).
        if (await _borrowers.HasHistoryAsync(id))
        {
            TempData["ErrorMessage"] = $"'{borrower.FullName}' has loan or reservation history, which is kept for the fine audit, so they can't be deleted.";
            return RedirectToAction(nameof(Index));
        }

        await _borrowers.DeleteAsync(borrower);
        TempData["StatusMessage"] = $"'{borrower.FullName}' was removed.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Records a fine as paid at the desk.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PayFine(int loanId, int borrowerId)
    {
        try
        {
            var loan = await _loanService.PayFineAsync(loanId);
            TempData["StatusMessage"] = $"Payment of ${loan.FineAmount:0.00} recorded for '{loan.Item.Name}'.";
        }
        catch (BusinessRuleException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = borrowerId });
    }

    private async Task PopulateBranchesAsync()
    {
        ViewBag.Branches = await _branches.GetAllAsync();
    }
}
