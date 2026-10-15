using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;
using LibraryManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagementSystem.Controllers;

/// <summary>
/// Lets any staff member choose which branch desk they're working at for
/// this session (Group Project - Multi-Branch Inventory). Kept separate from
/// BranchesController because that one is Admin-only, and ASP.NET Core
/// combines class and action [Authorize] attributes (both must pass).
/// </summary>
[Authorize(Roles = Roles.Admin + "," + Roles.Reception + "," + Roles.Manager)]
public class DeskController : Controller
{
    private readonly IBranchRepository _branches;
    private readonly IBranchContext _branchContext;

    public DeskController(IBranchRepository branches, IBranchContext branchContext)
    {
        _branches = branches;
        _branchContext = branchContext;
    }

    public async Task<IActionResult> Index(string? returnUrl)
    {
        ViewBag.ReturnUrl = returnUrl;
        ViewBag.Current = _branchContext.CurrentBranchCode;
        return View(await _branches.GetAllAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(string branchCode, string? returnUrl)
    {
        var branch = await _branches.GetByCodeAsync(branchCode ?? string.Empty);
        if (branch is null)
        {
            TempData["ErrorMessage"] = "Please choose a valid branch.";
            return RedirectToAction(nameof(Index));
        }

        _branchContext.SetDeskBranch(branch.Code);
        TempData["StatusMessage"] = $"You're now working at the {branch.Name} desk.";

        // Only redirect back inside this site (open-redirect protection, as in AccountController).
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction("Index", "Home");
    }
}
