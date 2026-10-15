using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Repositories;

public interface IBranchRepository
{
    Task<List<Branch>> GetAllAsync();
    Task<Branch?> GetByIdAsync(int id);
    Task<Branch?> GetByCodeAsync(string code);
    Task<bool> CodeExistsAsync(string code, int excludeId = 0);
    Task AddAsync(Branch branch);
    Task UpdateAsync(Branch branch);
}
