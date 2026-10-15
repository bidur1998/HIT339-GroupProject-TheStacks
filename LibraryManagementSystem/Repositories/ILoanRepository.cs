using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Repositories;

public interface ILoanRepository
{
    Task<List<Loan>> GetAllAsync();
    Task<Loan?> GetActiveLoanForItemAsync(int itemId);
    Task<Loan?> GetByIdAsync(int id);
    Task AddAsync(Loan loan);
    Task UpdateAsync(Loan loan);

    // --- Group Project additions ---

    /// <summary>Every loan not yet returned, with item and borrower loaded (used by the reminder job).</summary>
    Task<List<Loan>> GetActiveLoansAsync();
}
