namespace LibraryManagementSystem.Models.ViewModels;

/// <summary>
/// Everything the manager dashboard needs (requirement 3): borrowing
/// statistics, item status statistics and fine statistics, all assembled
/// by IStatisticsService so the controller and view stay simple.
/// </summary>
public class ManagerDashboardViewModel
{
    // Borrowing statistics
    public int TotalLoansEver { get; set; }
    public int CurrentlyBorrowed { get; set; }
    public int CurrentlyOverdue { get; set; }
    public List<TopItemStat> MostBorrowedItems { get; set; } = new();

    // Item status statistics
    public int TotalItems { get; set; }
    public Dictionary<ItemStatus, int> ItemsByStatus { get; set; } = new();
    public Dictionary<string, int> ItemsByType { get; set; } = new();

    // Fine statistics
    public decimal TotalFinesIssued { get; set; }
    public decimal TotalFinesCollected { get; set; }
    public decimal TotalFinesOutstanding { get; set; }
    public int LoansWithFines { get; set; }
}

public class TopItemStat
{
    public string Name { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public int TimesBorrowed { get; set; }
}
