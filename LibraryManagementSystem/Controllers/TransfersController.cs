using LibraryManagementSystem.Models;
using LibraryManagementSystem.Models.ViewModels;
using LibraryManagementSystem.Repositories;
using LibraryManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagementSystem.Controllers;

/// <summary>
/// Inter-branch transfers (Group Project - Multi-Branch Inventory). Staff
/// see what their desk needs to send out and what is on its way to them,
/// and can request new transfers. Reception may only dispatch from / receive
/// at their own desk branch; admins can act for any branch.
/// </summary>
[Authorize(Roles = Roles.Reception + "," + Roles.Admin)]
public class TransfersController : Controller
{
    private readonly ITransferService _transferService;
    private readonly ITransferRepository _transfers;
    private readonly IItemRepository _items;
    private readonly IBranchRepository _branches;
    private readonly IBranchContext _branchContext;

    public TransfersController(
        ITransferService transferService,
        ITransferRepository transfers,
        IItemRepository items,
        IBranchRepository branches,
        IBranchContext branchContext)
    {
        _transferService = transferService;
        _transfers = transfers;
        _items = items;
        _branches = branches;
        _branchContext = branchContext;
    }

    public async Task<IActionResult> Index()
    {
        var desk = await _branchContext.GetCurrentBranchAsync();
        var all = await _transfers.GetAllAsync();

        var model = new TransfersIndexViewModel
        {
            DeskBranch = desk,
            Branches = await _branches.GetAllAsync(),
            ToDispatch = all.Where(t => t.Status == TransferStatus.Requested && t.FromBranchId == desk?.Id).ToList(),
            ToReceive = all.Where(t => t.Status == TransferStatus.InTransit && t.ToBranchId == desk?.Id).ToList(),
            OtherOpen = all.Where(t => t.IsOpen
                                       && !(t.Status == TransferStatus.Requested && t.FromBranchId == desk?.Id)
                                       && !(t.Status == TransferStatus.InTransit && t.ToBranchId == desk?.Id)).ToList(),
            RecentlyClosed = all.Where(t => !t.IsOpen).Take(20).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestTransfer([Bind(Prefix = "NewRequest")] TransferRequestViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Enter a library code and choose a destination branch.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var item = await _items.GetByLibraryCodeAsync(model.LibraryCode)
                ?? throw new TransferException($"No item found with library code '{model.LibraryCode}'.");
            var transfer = await _transferService.RequestAsync(item.Id, model.ToBranchId, StaffName, model.Reason);
            var to = await _branches.GetByIdAsync(transfer.ToBranchId);
            TempData["StatusMessage"] = $"Transfer requested: '{item.Name}' to {to?.Name}.";
        }
        catch (BusinessRuleException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dispatch(int id) =>
        await RunAsync(async () =>
        {
            var t = await _transferService.DispatchAsync(id, StaffName, await ActingBranchIdAsync());
            return $"'{t.Item.Name}' marked as dispatched to {t.ToBranch.Name}.";
        });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Receive(int id) =>
        await RunAsync(async () =>
        {
            var t = await _transferService.ReceiveAsync(id, StaffName, await ActingBranchIdAsync());
            return $"'{t.Item.Name}' received at {t.ToBranch.Name}. If it was reserved, the patron has been notified.";
        });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id) =>
        await RunAsync(async () =>
        {
            var t = await _transferService.CancelAsync(id);
            return $"Transfer of '{t.Item.Name}' cancelled.";
        });

    private async Task<IActionResult> RunAsync(Func<Task<string>> action)
    {
        try
        {
            TempData["StatusMessage"] = await action();
        }
        catch (BusinessRuleException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Admins can act for any branch (null = no check); reception only at their desk branch.</summary>
    private async Task<int?> ActingBranchIdAsync() =>
        User.IsInRole(Roles.Admin) ? null : (await _branchContext.GetCurrentBranchAsync())?.Id;

    private string StaffName => User.Identity?.Name ?? "staff";
}
