using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Repositories;

public interface ITransferRepository
{
    Task<List<BranchTransfer>> GetAllAsync();
    Task<BranchTransfer?> GetByIdAsync(int id);
    Task<BranchTransfer?> GetOpenForItemAsync(int itemId);
    Task AddAsync(BranchTransfer transfer);
    Task UpdateAsync(BranchTransfer transfer);
}
