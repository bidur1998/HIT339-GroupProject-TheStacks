using LibraryManagementSystem.Models.ViewModels;
using LibraryManagementSystem.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagementSystem.Controllers;

/// <summary>
/// The public, phone-friendly search portal (requirement 4). Deliberately
/// anonymous - members of the public never need an account to look up
/// whether something is on the shelf.
///
/// Group Project: results show which branch holds each item, can be
/// filtered by branch, and link to "Place a hold" for items that are out.
/// </summary>
[AllowAnonymous]
public class SearchController : Controller
{
    private readonly IItemRepository _items;
    private readonly IBranchRepository _branches;

    public SearchController(IItemRepository items, IBranchRepository branches)
    {
        _items = items;
        _branches = branches;
    }

    public async Task<IActionResult> Index(string? query, string? typeFilter, bool availableOnly = false, int? branchId = null)
    {
        var model = new SearchViewModel
        {
            Query = query,
            TypeFilter = typeFilter,
            AvailableOnly = availableOnly,
            BranchId = branchId,
            Branches = await _branches.GetAllAsync()
        };

        // Only actually run a search once the visitor has typed something
        // or applied a filter, so the page doesn't dump the whole
        // catalogue on first load - friendlier on a small phone screen.
        if (!string.IsNullOrWhiteSpace(query) || !string.IsNullOrWhiteSpace(typeFilter) || availableOnly || branchId.HasValue)
        {
            model.Results = await _items.SearchAsync(query, typeFilter, availableOnly, branchId);
        }

        return View(model);
    }
}
