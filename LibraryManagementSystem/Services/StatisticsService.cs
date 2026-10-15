using LibraryManagementSystem.Models;
using LibraryManagementSystem.Models.ViewModels;
using LibraryManagementSystem.Repositories;

namespace LibraryManagementSystem.Services;

public class StatisticsService : IStatisticsService
{
    private readonly IItemRepository _items;
    private readonly ILoanRepository _loans;

    public StatisticsService(IItemRepository items, ILoanRepository loans)
    {
        _items = items;
        _loans = loans;
    }

    public async Task<ManagerDashboardViewModel> BuildDashboardAsync()
    {
        var items = await _items.GetAllAsync();
        var loans = await _loans.GetAllAsync();

        var vm = new ManagerDashboardViewModel
        {
            // --- Borrowing statistics ---
            TotalLoansEver = loans.Count,
            CurrentlyBorrowed = loans.Count(l => !l.IsReturned),
            CurrentlyOverdue = loans.Count(l => l.IsOverdue),
            MostBorrowedItems = loans
                .GroupBy(l => l.Item)
                .Select(g => new TopItemStat
                {
                    Name = g.Key.Name,
                    ItemType = g.Key.ItemType,
                    TimesBorrowed = g.Count()
                })
                .OrderByDescending(s => s.TimesBorrowed)
                .Take(5)
                .ToList(),

            // --- Item status statistics ---
            TotalItems = items.Count,
            ItemsByStatus = Enum.GetValues<ItemStatus>()
                .ToDictionary(s => s, s => items.Count(i => i.Status == s)),
            ItemsByType = items
                .GroupBy(i => i.ItemType)
                .ToDictionary(g => g.Key, g => g.Count()),

            // --- Fine statistics ---
            TotalFinesIssued = loans.Sum(l => l.FineAmount ?? 0m),
            TotalFinesCollected = loans.Where(l => l.FinePaid).Sum(l => l.FineAmount ?? 0m),
            LoansWithFines = loans.Count(l => (l.FineAmount ?? 0m) > 0)
        };

        vm.TotalFinesOutstanding = vm.TotalFinesIssued - vm.TotalFinesCollected;

        return vm;
    }
}
