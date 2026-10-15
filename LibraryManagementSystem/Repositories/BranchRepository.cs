using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Repositories;

public class BranchRepository : IBranchRepository
{
    private readonly ApplicationDbContext _context;

    public BranchRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Branch>> GetAllAsync() =>
        await _context.Branches.OrderBy(b => b.Name).ToListAsync();

    public async Task<Branch?> GetByIdAsync(int id) =>
        await _context.Branches.FirstOrDefaultAsync(b => b.Id == id);

    public async Task<Branch?> GetByCodeAsync(string code) =>
        await _context.Branches.FirstOrDefaultAsync(b => b.Code.ToUpper() == code.Trim().ToUpper());

    public async Task<bool> CodeExistsAsync(string code, int excludeId = 0) =>
        await _context.Branches.AnyAsync(b => b.Code.ToUpper() == code.Trim().ToUpper() && b.Id != excludeId);

    public async Task AddAsync(Branch branch)
    {
        _context.Branches.Add(branch);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Branch branch)
    {
        _context.Branches.Update(branch);
        await _context.SaveChangesAsync();
    }
}
