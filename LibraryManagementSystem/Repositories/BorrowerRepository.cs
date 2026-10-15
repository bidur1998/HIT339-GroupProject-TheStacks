using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Repositories;

public class BorrowerRepository : IBorrowerRepository
{
    private readonly ApplicationDbContext _context;

    public BorrowerRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Borrower>> GetAllAsync() =>
        await _context.Borrowers
            .Include(b => b.Loans)
            .OrderBy(b => b.FullName)
            .ToListAsync();

    public async Task<Borrower?> GetByIdAsync(int id) =>
        await BorrowerWithAccount
            .FirstOrDefaultAsync(b => b.Id == id);

    // Group Project: a borrower's "account" now also includes their home
    // branch and reservations, for the kiosk account summary and reception.
    private IQueryable<Borrower> BorrowerWithAccount =>
        _context.Borrowers
            .Include(b => b.HomeBranch)
            .Include(b => b.Loans).ThenInclude(l => l.Item)
            .Include(b => b.Reservations).ThenInclude(r => r.Item)
            .Include(b => b.Reservations).ThenInclude(r => r.PickupBranch);

    public async Task<Borrower?> GetByCardNumberAsync(string cardNumber) =>
        await BorrowerWithAccount
            .FirstOrDefaultAsync(b => b.CardNumber != null && b.CardNumber.ToUpper() == cardNumber.Trim().ToUpper());

    public async Task<bool> CardNumberExistsAsync(string cardNumber) =>
        await _context.Borrowers.AnyAsync(b => b.CardNumber != null && b.CardNumber.ToUpper() == cardNumber.Trim().ToUpper());

    public async Task<bool> HasHistoryAsync(int borrowerId) =>
        await _context.Loans.AnyAsync(l => l.BorrowerId == borrowerId)
        || await _context.Reservations.AnyAsync(r => r.BorrowerId == borrowerId);

    public async Task AddAsync(Borrower borrower)
    {
        _context.Borrowers.Add(borrower);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Borrower borrower)
    {
        _context.Borrowers.Update(borrower);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Borrower borrower)
    {
        _context.Borrowers.Remove(borrower);
        await _context.SaveChangesAsync();
    }
}
