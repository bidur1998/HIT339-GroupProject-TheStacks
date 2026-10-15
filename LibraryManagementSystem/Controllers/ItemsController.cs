using LibraryManagementSystem.Models;
using LibraryManagementSystem.Models.ViewModels;
using LibraryManagementSystem.Repositories;
using LibraryManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagementSystem.Controllers;

/// <summary>
/// The admin account's item CRUD screens (requirement 1). One controller,
/// one form, three possible underlying types - the ItemType field on
/// ItemFormViewModel decides which of Book/Toy/Music actually gets built.
///
/// Group Project: items now have a home branch and current location, the
/// list can be filtered by branch, and changing an item's status (e.g.
/// Damaged -> Available after a repair) notifies the reservation queue.
/// </summary>
[Authorize(Roles = Roles.Admin)]
public class ItemsController : Controller
{
    private readonly IItemRepository _items;
    private readonly IBranchRepository _branches;
    private readonly IReservationRepository _reservations;
    private readonly IReservationService _reservationService;

    public ItemsController(
        IItemRepository items,
        IBranchRepository branches,
        IReservationRepository reservations,
        IReservationService reservationService)
    {
        _items = items;
        _branches = branches;
        _reservations = reservations;
        _reservationService = reservationService;
    }

    public async Task<IActionResult> Index(string? typeFilter, int? branchId)
    {
        var items = await _items.GetAllAsync();
        if (!string.IsNullOrWhiteSpace(typeFilter) && typeFilter != "All")
        {
            items = items.Where(i => i.ItemType == typeFilter).ToList();
        }
        if (branchId.HasValue)
        {
            items = items.Where(i => i.CurrentBranchId == branchId).ToList();
        }
        ViewBag.TypeFilter = typeFilter ?? "All";
        ViewBag.BranchId = branchId;
        ViewBag.Branches = await _branches.GetAllAsync();
        return View(items);
    }

    public async Task<IActionResult> Details(int id)
    {
        var item = await _items.GetByIdAsync(id);
        if (item is null) return NotFound();
        ViewBag.Queue = await _reservations.GetActiveQueueForItemAsync(id);
        return View(item);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateBranchesAsync();
        var defaultBranch = (await _branches.GetAllAsync()).FirstOrDefault();
        return View(new ItemFormViewModel { HomeBranchId = defaultBranch?.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ItemFormViewModel model)
    {
        await ValidateModelAsync(model);

        if (model.Status == ItemStatus.Borrowed)
        {
            ModelState.AddModelError(nameof(model.Status), "A new item can't start as Borrowed - lend it through Reception.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateBranchesAsync();
            return View(model);
        }

        var item = BuildItemFromViewModel(model, existing: null);
        item.CurrentBranchId = item.HomeBranchId; // a new item starts at its home branch
        await _items.AddAsync(item);

        TempData["StatusMessage"] = $"'{item.Name}' was added to the catalogue.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await _items.GetByIdAsync(id);
        if (item is null) return NotFound();
        await PopulateBranchesAsync();
        return View(ItemFormViewModel.FromItem(item));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ItemFormViewModel model)
    {
        if (id != model.Id) return BadRequest();

        var existing = await _items.GetByIdAsync(id);
        if (existing is null) return NotFound();

        await ValidateModelAsync(model, excludeId: id);

        // Borrowed is controlled by Reception's borrow/return screens only,
        // otherwise an item could be "borrowed" with no loan record.
        var oldStatus = existing.Status;
        if (oldStatus == ItemStatus.Borrowed && model.Status != ItemStatus.Borrowed)
        {
            ModelState.AddModelError(nameof(model.Status), "This item is on loan - use Reception > Return to check it back in.");
        }
        else if (oldStatus != ItemStatus.Borrowed && model.Status == ItemStatus.Borrowed)
        {
            ModelState.AddModelError(nameof(model.Status), "Use Reception > Borrow to lend an item out.");
        }

        if (!ModelState.IsValid)
        {
            model.CurrentBranchName = existing.CurrentBranch?.Name;
            await PopulateBranchesAsync();
            return View(model);
        }

        // The item type is fixed once created - only its details can be edited.
        model.ItemType = existing.ItemType;
        var updated = BuildItemFromViewModel(model, existing);
        updated.CurrentBranchId ??= updated.HomeBranchId;
        await _items.UpdateAsync(updated);

        // Reservation queue reacts to status changes made here too.
        string extra = string.Empty;
        if (oldStatus != ItemStatus.Available && updated.Status == ItemStatus.Available)
        {
            var next = await _reservationService.OnItemBecameAvailableAsync(updated);
            if (next is not null)
            {
                extra = next.Status == ReservationStatus.ReadyForPickup
                    ? $" It's reserved - {next.Borrower.FullName} has been told it's ready to collect."
                    : " It's reserved for pickup at another branch, so a transfer has been requested.";
            }
        }
        else if (oldStatus != ItemStatus.Destroyed && updated.Status == ItemStatus.Destroyed)
        {
            int cancelled = await _reservationService.OnItemWithdrawnAsync(updated, "the item has been withdrawn from the collection.");
            if (cancelled > 0)
            {
                extra = $" {cancelled} reservation(s) were cancelled and the patrons notified.";
            }
        }

        TempData["StatusMessage"] = $"'{updated.Name}' was updated.{extra}";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var item = await _items.GetByIdAsync(id);
        if (item is null) return NotFound();
        return View(item);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await _items.GetByIdAsync(id);
        if (item is null) return NotFound();

        if (item.Status == ItemStatus.Borrowed)
        {
            TempData["ErrorMessage"] = $"'{item.Name}' is currently on loan and cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        // Fix found while planning the Group Project: in Assignment 2 an item
        // with past loans hit a database error here (Restrict foreign key).
        // Now the admin gets a clear message instead.
        if (await _items.HasHistoryAsync(id))
        {
            TempData["ErrorMessage"] = $"'{item.Name}' has loan, reservation or transfer history, so it can't be deleted. Set its status to Destroyed to withdraw it instead.";
            return RedirectToAction(nameof(Index));
        }

        await _items.DeleteAsync(item);
        TempData["StatusMessage"] = $"'{item.Name}' was removed from the catalogue.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateBranchesAsync()
    {
        ViewBag.Branches = await _branches.GetAllAsync();
    }

    private async Task ValidateModelAsync(ItemFormViewModel model, int excludeId = 0)
    {
        if (await _items.LibraryCodeExistsAsync(model.LibraryCode, excludeId))
        {
            ModelState.AddModelError(nameof(model.LibraryCode), "That library code is already in use.");
        }

        if (model.HomeBranchId.HasValue && await _branches.GetByIdAsync(model.HomeBranchId.Value) is null)
        {
            ModelState.AddModelError(nameof(model.HomeBranchId), "Please choose a valid branch.");
        }

        switch (model.ItemType)
        {
            case "Book" when string.IsNullOrWhiteSpace(model.Author):
                ModelState.AddModelError(nameof(model.Author), "Please enter the author.");
                break;
            case "Music" when string.IsNullOrWhiteSpace(model.Artist):
                ModelState.AddModelError(nameof(model.Artist), "Please enter the artist.");
                break;
            case "Toy" when string.IsNullOrWhiteSpace(model.ToyType):
                ModelState.AddModelError(nameof(model.ToyType), "Please enter the toy type.");
                break;
        }
    }

    private static Item BuildItemFromViewModel(ItemFormViewModel model, Item? existing)
    {
        Item item = existing ?? model.ItemType switch
        {
            "Book" => new Book(),
            "Toy" => new Toy(),
            "Music" => new Music(),
            _ => throw new InvalidOperationException($"Unknown item type '{model.ItemType}'.")
        };

        item.Name = model.Name.Trim();
        item.Description = model.Description?.Trim();
        item.LibraryCode = model.LibraryCode.Trim().ToUpperInvariant();
        item.Status = model.Status;
        item.HomeBranchId = model.HomeBranchId;

        switch (item)
        {
            case Book book:
                book.Author = model.Author?.Trim() ?? string.Empty;
                book.Genre = model.Genre?.Trim();
                break;
            case Music music:
                music.Artist = model.Artist?.Trim() ?? string.Empty;
                music.Year = model.Year;
                break;
            case Toy toy:
                toy.ToyType = model.ToyType?.Trim() ?? string.Empty;
                toy.RecommendedAge = model.RecommendedAge;
                break;
        }

        return item;
    }
}
