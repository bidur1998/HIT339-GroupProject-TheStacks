using LibraryManagementSystem.Models;
using LibraryManagementSystem.Models.ViewModels;
using LibraryManagementSystem.Repositories;
using LibraryManagementSystem.Services;
using LibraryManagementSystem.Services.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagementSystem.Controllers;

/// <summary>
/// The manager's read-only statistics dashboard (requirement 3).
/// Group Project: adds filterable reports (borrowing, fine revenue audit,
/// inventory health) with CSV and PDF export.
/// </summary>
[Authorize(Roles = Roles.Manager)]
public class ManagerController : Controller
{
    private readonly IStatisticsService _statistics;
    private readonly IReportService _reports;
    private readonly IEnumerable<IReportExporter> _exporters;
    private readonly IBranchRepository _branches;

    public ManagerController(
        IStatisticsService statistics,
        IReportService reports,
        IEnumerable<IReportExporter> exporters,
        IBranchRepository branches)
    {
        _statistics = statistics;
        _reports = reports;
        _exporters = exporters;
        _branches = branches;
    }

    public async Task<IActionResult> Dashboard()
    {
        var model = await _statistics.BuildDashboardAsync();
        return View(model);
    }

    public async Task<IActionResult> Reports(ReportType reportType = ReportType.Borrowing, int? branchId = null, DateTime? from = null, DateTime? to = null)
    {
        var model = new ReportsViewModel
        {
            ReportType = reportType,
            BranchId = branchId,
            From = from,
            To = to,
            Branches = await _branches.GetAllAsync()
        };

        if (from.HasValue && to.HasValue && from > to)
        {
            ModelState.AddModelError(string.Empty, "The 'from' date must be on or before the 'to' date.");
            return View(model);
        }

        model.Report = await _reports.BuildAsync(reportType, new ReportFilter { BranchId = branchId, From = from, To = to });
        return View(model);
    }

    public async Task<IActionResult> Export(string format, ReportType reportType = ReportType.Borrowing, int? branchId = null, DateTime? from = null, DateTime? to = null)
    {
        var exporter = _exporters.FirstOrDefault(e => e.Format.Equals(format ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        if (exporter is null)
        {
            return BadRequest("Format must be csv or pdf.");
        }

        if (from.HasValue && to.HasValue && from > to)
        {
            return BadRequest("The 'from' date must be on or before the 'to' date.");
        }

        var report = await _reports.BuildAsync(reportType, new ReportFilter { BranchId = branchId, From = from, To = to });
        var bytes = exporter.Export(report);
        string fileName = $"{reportType}-report-{DateTime.UtcNow:yyyyMMdd-HHmm}.{exporter.FileExtension}";
        return File(bytes, exporter.ContentType, fileName);
    }
}
