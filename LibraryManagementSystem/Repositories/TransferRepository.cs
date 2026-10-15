using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Repositories;

public class TransferRepository : ITransferRepository
{
    private readonly ApplicationDbContext _context;

    public TransferRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    private IQueryable<BranchTransfer> WithDetails =>
        _context.BranchTransfers
            .Include(t => t.Item)
            .Include(t => t.FromBranch)
            .Include(t => t.ToBranch);

    public async Task<List<BranchTransfer>> GetAllAsync() =>
        await WithDetails.OrderByDescending(t => t.RequestedAt).ToListAsync();

    public async Task<BranchTransfer?> GetByIdAsync(int id) =>
        await WithDetails.FirstOrDefaultAsync(t => t.Id == id);

    public async Task<BranchTransfer?> GetOpenForItemAsync(int itemId) =>
        await WithDetails.FirstOrDefaultAsync(t =>
            t.ItemId == itemId &&
            (t.Status == TransferStatus.Requested || t.Status == TransferStatus.InTransit));

    public async Task AddAsync(BranchTransfer transfer)
    {
        _context.BranchTransfers.Add(transfer);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(BranchTransfer transfer)
    {
        _context.BranchTransfers.Update(transfer);
        await _context.SaveChangesAsync();
    }
}
