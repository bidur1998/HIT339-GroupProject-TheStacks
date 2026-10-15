using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Repositories;

public class LoanRepository : ILoanRepository
{
    private readonly ApplicationDbContext _context;

    public LoanRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Loan>> GetAllAsync() =>
        await _context.Loans
            .Include(l => l.Item)
            .Include(l => l.Borrower)
            .Include(l => l.BorrowBranch)
            .Include(l => l.ReturnBranch)
            .OrderByDescending(l => l.BorrowedDate)
            .ToListAsync();

    public async Task<Loan?> GetActiveLoanForItemAsync(int itemId) =>
        await _context.Loans
            .Include(l => l.Item)
            .Include(l => l.Borrower)
            .Where(l => l.ItemId == itemId && l.ReturnedDate == null)
            .OrderByDescending(l => l.BorrowedDate)
            .FirstOrDefaultAsync();

    public async Task<Loan?> GetByIdAsync(int id) =>
        await _context.Loans
            .Include(l => l.Item)
            .Include(l => l.Borrower)
            .FirstOrDefaultAsync(l => l.Id == id);

    public async Task AddAsync(Loan loan)
    {
        _context.Loans.Add(loan);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Loan loan)
    {
        _context.Loans.Update(loan);
        await _context.SaveChangesAsync();
    }

    public async Task<List<Loan>> GetActiveLoansAsync() =>
        await _context.Loans
            .Include(l => l.Item)
            .Include(l => l.Borrower)
            .Where(l => l.ReturnedDate == null)
            .OrderBy(l => l.DueDate)
            .ToListAsync();
}
