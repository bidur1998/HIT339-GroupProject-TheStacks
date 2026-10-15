using LibraryManagementSystem.Models.ViewModels;

namespace LibraryManagementSystem.Services;

/// <summary>Assembles the borrowing, item-status and fine statistics required by requirement 3.</summary>
public interface IStatisticsService
{
    Task<ManagerDashboardViewModel> BuildDashboardAsync();
}
