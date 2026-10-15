using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Repositories;

/// <summary>
/// The abstraction that controllers and services depend on instead of
/// talking to EF Core directly. This is the Dependency Inversion Principle
/// in practice (see the DIP reading set for this unit): high-level modules
/// - the controllers - depend on this interface, not on the concrete
/// ItemRepository or on ApplicationDbContext, so the data-access details
/// can change without the controllers ever knowing.
/// </summary>
public interface IItemRepository
{
    Task<List<Item>> GetAllAsync();
    Task<Item?> GetByIdAsync(int id);
    Task<Item?> GetByLibraryCodeAsync(string libraryCode);
    Task<List<Item>> SearchAsync(string? query, string? typeFilter, bool availableOnly, int? branchId = null);
    Task AddAsync(Item item);
    Task UpdateAsync(Item item);
    Task DeleteAsync(Item item);
    Task<bool> LibraryCodeExistsAsync(string libraryCode, int excludeId = 0);

    // --- Group Project additions ---

    /// <summary>Bulk insert used by the CSV / external metadata importer - one SaveChanges for the whole batch.</summary>
    Task AddRangeAsync(IEnumerable<Item> items);

    /// <summary>Which of these codes are already used (case-insensitive), so the importer can reject duplicates.</summary>
    Task<HashSet<string>> GetExistingLibraryCodesAsync(IEnumerable<string> libraryCodes);

    /// <summary>True if the item has loans, reservations or transfers on record (so it must not be hard-deleted).</summary>
    Task<bool> HasHistoryAsync(int itemId);
}
