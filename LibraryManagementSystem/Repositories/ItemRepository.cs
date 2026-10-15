using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Repositories;

/// <summary>
/// The concrete, EF Core-backed implementation of IItemRepository. This is
/// the "detail" that depends on the "abstraction" (the interface), exactly
/// as the Dependency Inversion Principle describes - not the other way
/// around.
/// </summary>
public class ItemRepository : IItemRepository
{
    private readonly ApplicationDbContext _context;

    public ItemRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    // Group Project: every item query now also loads which branch owns the
    // item and which branch is physically holding it.
    private IQueryable<Item> ItemsWithBranches =>
        _context.Items
            .Include(i => i.HomeBranch)
            .Include(i => i.CurrentBranch);

    public async Task<List<Item>> GetAllAsync() =>
        await ItemsWithBranches.OrderBy(i => i.Name).ToListAsync();

    public async Task<Item?> GetByIdAsync(int id) =>
        await ItemsWithBranches.FirstOrDefaultAsync(i => i.Id == id);

    public async Task<Item?> GetByLibraryCodeAsync(string libraryCode) =>
        await ItemsWithBranches.FirstOrDefaultAsync(i =>
            i.LibraryCode.ToLower() == libraryCode.Trim().ToLower());

    public async Task<List<Item>> SearchAsync(string? query, string? typeFilter, bool availableOnly, int? branchId = null)
    {
        var items = ItemsWithBranches;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim().ToLower();
            items = items.Where(i =>
                i.Name.ToLower().Contains(q) ||
                i.LibraryCode.ToLower().Contains(q) ||
                (i.Description != null && i.Description.ToLower().Contains(q)) ||
                (i is Book && ((Book)i).Author.ToLower().Contains(q)) ||
                (i is Music && ((Music)i).Artist.ToLower().Contains(q)));
        }

        if (!string.IsNullOrWhiteSpace(typeFilter) && typeFilter != "All")
        {
            items = typeFilter switch
            {
                "Book" => items.Where(i => i is Book),
                "Toy" => items.Where(i => i is Toy),
                "Music" => items.Where(i => i is Music),
                _ => items
            };
        }

        if (availableOnly)
        {
            items = items.Where(i => i.Status == ItemStatus.Available);
        }

        if (branchId.HasValue)
        {
            items = items.Where(i => i.CurrentBranchId == branchId.Value);
        }

        return await items.OrderBy(i => i.Name).ToListAsync();
    }

    public async Task AddAsync(Item item)
    {
        _context.Items.Add(item);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Item item)
    {
        _context.Items.Update(item);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Item item)
    {
        _context.Items.Remove(item);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> LibraryCodeExistsAsync(string libraryCode, int excludeId = 0) =>
        await _context.Items.AnyAsync(i =>
            i.LibraryCode.ToLower() == libraryCode.Trim().ToLower() && i.Id != excludeId);

    public async Task AddRangeAsync(IEnumerable<Item> items)
    {
        // One SaveChanges = one database transaction, so an import either
        // adds every valid row or none of them.
        _context.Items.AddRange(items);
        await _context.SaveChangesAsync();
    }

    public async Task<HashSet<string>> GetExistingLibraryCodesAsync(IEnumerable<string> libraryCodes)
    {
        var wanted = libraryCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpper())
            .Distinct()
            .ToList();

        var existing = await _context.Items
            .Where(i => wanted.Contains(i.LibraryCode.ToUpper()))
            .Select(i => i.LibraryCode.ToUpper())
            .ToListAsync();

        return new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> HasHistoryAsync(int itemId) =>
        await _context.Loans.AnyAsync(l => l.ItemId == itemId)
        || await _context.Reservations.AnyAsync(r => r.ItemId == itemId)
        || await _context.BranchTransfers.AnyAsync(t => t.ItemId == itemId);
}
