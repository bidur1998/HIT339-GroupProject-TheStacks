using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Repositories;

public interface IBorrowerRepository
{
    Task<List<Borrower>> GetAllAsync();
    Task<Borrower?> GetByIdAsync(int id);
    Task AddAsync(Borrower borrower);
    Task UpdateAsync(Borrower borrower);
    Task DeleteAsync(Borrower borrower);

    // --- Group Project additions ---
    Task<Borrower?> GetByCardNumberAsync(string cardNumber);
    Task<bool> CardNumberExistsAsync(string cardNumber);

    /// <summary>True if the borrower has loans or reservations on record (kept for the audit trail).</summary>
    Task<bool> HasHistoryAsync(int borrowerId);
}
