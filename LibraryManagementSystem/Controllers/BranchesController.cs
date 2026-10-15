using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;
using LibraryManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagementSystem.Controllers;

/// <summary>
/// Branch management (Group Project - Multi-Branch Inventory). Admins add
/// and edit branches (opening hours, temporary closures). Switching the
/// desk branch is in DeskController, because any staff role can do that.
/// </summary>
[Authorize(Roles = Roles.Admin)]
public class BranchesController : Controller
{
    private readonly IBranchRepository _branches;
    private readonly IItemRepository _items;
    private readonly IBranchContext _branchContext;

    public BranchesController(IBranchRepository branches, IItemRepository items, IBranchContext branchContext)
    {
        _branches = branches;
        _items = items;
        _branchContext = branchContext;
    }

    public async Task<IActionResult> Index()
    {
        var branches = await _branches.GetAllAsync();
        var items = await _items.GetAllAsync();
        ViewBag.ItemCounts = branches.ToDictionary(b => b.Id, b => items.Count(i => i.CurrentBranchId == b.Id));
        ViewBag.LocalNow = _branchContext.LocalNow;
        return View(branches);
    }

    public IActionResult Create() => View(new Branch());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Branch branch)
    {
        branch.Id = 0; // never trust a posted id on create
        branch.Code = (branch.Code ?? string.Empty).Trim().ToUpperInvariant();
        await ValidateAsync(branch);
        if (!ModelState.IsValid) return View(branch);

        await _branches.AddAsync(branch);
        TempData["StatusMessage"] = $"Branch '{branch.Name}' ({branch.Code}) was added.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var branch = await _branches.GetByIdAsync(id);
        if (branch is null) return NotFound();
        return View(branch);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Branch form)
    {
        if (id != form.Id) return BadRequest();

        var branch = await _branches.GetByIdAsync(id);
        if (branch is null) return NotFound();

        form.Code = (form.Code ?? string.Empty).Trim().ToUpperInvariant();
        await ValidateAsync(form);
        if (!ModelState.IsValid) return View(form);

        // Copy the editable fields onto the tracked entity.
        branch.Code = form.Code;
        branch.Name = form.Name.Trim();
        branch.Address = form.Address;
        branch.PhoneNumber = form.PhoneNumber;
        branch.OpeningTime = form.OpeningTime;
        branch.ClosingTime = form.ClosingTime;
        branch.OpenOnWeekends = form.OpenOnWeekends;
        branch.OperatingStatus = form.OperatingStatus;
        branch.StatusNote = form.StatusNote;

        await _branches.UpdateAsync(branch);
        TempData["StatusMessage"] = $"Branch '{branch.Name}' was updated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateAsync(Branch branch)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(branch.Code, "^[A-Z0-9]{2,10}$"))
        {
            ModelState.AddModelError(nameof(Branch.Code), "Use 2-10 capital letters or digits, e.g. DAR.");
        }

        if (await _branches.CodeExistsAsync(branch.Code, branch.Id))
        {
            ModelState.AddModelError(nameof(Branch.Code), "Another branch already uses that code.");
        }

        if (branch.ClosingTime <= branch.OpeningTime)
        {
            ModelState.AddModelError(nameof(Branch.ClosingTime), "Closing time must be after opening time.");
        }
    }
}
