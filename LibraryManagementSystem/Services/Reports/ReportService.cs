using System.Globalization;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;

namespace LibraryManagementSystem.Services.Reports;

/// <summary>
/// Expanded Manager Analytics &amp; Export (Group Project). Builds the three
/// reports the brief asks for: borrowing statistics, a fine revenue audit
/// and an inventory health report. Like the original StatisticsService, the
/// numbers are worked out from live data each time, never stored.
/// </summary>
public interface IReportService
{
    Task<Report> BuildAsync(ReportType type, ReportFilter filter);
}

public class ReportService : IReportService
{
    private static readonly CultureInfo Money = CultureInfo.GetCultureInfo("en-AU");

    private readonly IItemRepository _items;
    private readonly ILoanRepository _loans;
    private readonly IBranchRepository _branches;
    private readonly IReservationRepository _reservations;
    private readonly ITransferRepository _transfers;
    private readonly TimeProvider _time;

    public ReportService(
        IItemRepository items,
        ILoanRepository loans,
        IBranchRepository branches,
        IReservationRepository reservations,
        ITransferRepository transfers,
        TimeProvider time)
    {
        _items = items;
        _loans = loans;
        _branches = branches;
        _reservations = reservations;
        _transfers = transfers;
        _time = time;
    }

    public async Task<Report> BuildAsync(ReportType type, ReportFilter filter)
    {
        var branches = await _branches.GetAllAsync();
        var report = type switch
        {
            ReportType.Borrowing => await BorrowingAsync(filter, branches),
            ReportType.FineAudit => await FineAuditAsync(filter, branches),
            ReportType.InventoryHealth => await InventoryHealthAsync(filter, branches),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        report.GeneratedAtUtc = _time.GetUtcNow().UtcDateTime;
        report.Subtitle = DescribeFilter(filter, branches);
        return report;
    }

    // ------------------------------------------------------------------
    // 1. Borrowing statistics
    // ------------------------------------------------------------------
    private async Task<Report> BorrowingAsync(ReportFilter filter, List<Branch> branches)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var loans = (await _loans.GetAllAsync())
            .Where(l => filter.InRange(l.BorrowedDate))
            .Where(l => filter.BranchId is null || l.BorrowBranchId == filter.BranchId)
            .ToList();

        var returned = loans.Where(l => l.ReturnedDate.HasValue).ToList();
        double avgDays = returned.Count == 0 ? 0 : returned.Average(l => (l.ReturnedDate!.Value - l.BorrowedDate).TotalDays);

        var report = new Report { Title = "Borrowing statistics" };
        report.Summary.Add(new("Loans in period", loans.Count.ToString()));
        report.Summary.Add(new("Still out", loans.Count(l => !l.IsReturned).ToString()));
        report.Summary.Add(new("Overdue now", loans.Count(l => !l.IsReturned && now.Date > l.DueDate.Date).ToString()));
        report.Summary.Add(new("Returned late", returned.Count(l => l.ReturnedDate!.Value.Date > l.DueDate.Date).ToString()));
        report.Summary.Add(new("Average loan length (returned)", $"{avgDays:0.0} days"));
        report.Summary.Add(new("Desk / kiosk loans",
            $"{loans.Count(l => l.BorrowChannel != "Kiosk")} / {loans.Count(l => l.BorrowChannel == "Kiosk")}"));

        var byBranch = new ReportSection
        {
            Heading = "Loans by branch and item type",
            Columns = new() { "Branch", "Books", "Toys", "Music", "Total", "Kiosk share" }
        };
        foreach (var group in loans.GroupBy(l => BranchName(l.BorrowBranchId, branches)).OrderBy(g => g.Key))
        {
            int total = group.Count();
            int kiosk = group.Count(l => l.BorrowChannel == "Kiosk");
            byBranch.Rows.Add(new()
            {
                group.Key,
                group.Count(l => l.Item is Book).ToString(),
                group.Count(l => l.Item is Toy).ToString(),
                group.Count(l => l.Item is Music).ToString(),
                total.ToString(),
                total == 0 ? "0%" : $"{100.0 * kiosk / total:0}%"
            });
        }
        report.Sections.Add(byBranch);

        var topItems = new ReportSection
        {
            Heading = "Most borrowed items",
            Columns = new() { "Item", "Code", "Type", "Home branch", "Times borrowed" }
        };
        foreach (var group in loans.GroupBy(l => l.ItemId).OrderByDescending(g => g.Count()).ThenBy(g => g.First().Item.Name).Take(15))
        {
            var item = group.First().Item;
            topItems.Rows.Add(new()
            {
                item.Name, item.LibraryCode, item.ItemType, BranchName(item.HomeBranchId, branches), group.Count().ToString()
            });
        }
        report.Sections.Add(topItems);

        var monthly = new ReportSection
        {
            Heading = "Loans per month",
            Columns = new() { "Month", "Loans" }
        };
        foreach (var group in loans.GroupBy(l => new DateTime(l.BorrowedDate.Year, l.BorrowedDate.Month, 1)).OrderBy(g => g.Key))
        {
            monthly.Rows.Add(new() { group.Key.ToString("MMM yyyy", CultureInfo.InvariantCulture), group.Count().ToString() });
        }
        report.Sections.Add(monthly);

        return report;
    }

    // ------------------------------------------------------------------
    // 2. Fine revenue audit
    // ------------------------------------------------------------------
    private async Task<Report> FineAuditAsync(ReportFilter filter, List<Branch> branches)
    {
        // A fine is "issued" on the day the late item comes back, so the
        // period filter applies to the return date and the branch filter
        // to the branch that took the return.
        var fined = (await _loans.GetAllAsync())
            .Where(l => (l.FineAmount ?? 0) > 0)
            .Where(l => filter.InRange(l.ReturnedDate))
            .Where(l => filter.BranchId is null || l.ReturnBranchId == filter.BranchId)
            .OrderBy(l => l.ReturnedDate)
            .ToList();

        decimal issued = fined.Sum(l => l.FineAmount ?? 0);
        decimal collected = fined.Where(l => l.FinePaid).Sum(l => l.FineAmount ?? 0);

        var report = new Report { Title = "Fine revenue audit" };
        report.Summary.Add(new("Fines issued", $"{fined.Count} totalling {issued.ToString("C", Money)}"));
        report.Summary.Add(new("Collected", collected.ToString("C", Money)));
        report.Summary.Add(new("Outstanding", (issued - collected).ToString("C", Money)));
        report.Summary.Add(new("Collection rate", issued == 0 ? "n/a" : $"{100m * collected / issued:0}%"));

        var byBranch = new ReportSection
        {
            Heading = "Fine revenue by branch",
            Columns = new() { "Branch", "Fines", "Issued", "Collected", "Outstanding" }
        };
        foreach (var group in fined.GroupBy(l => BranchName(l.ReturnBranchId, branches)).OrderBy(g => g.Key))
        {
            decimal gIssued = group.Sum(l => l.FineAmount ?? 0);
            decimal gCollected = group.Where(l => l.FinePaid).Sum(l => l.FineAmount ?? 0);
            byBranch.Rows.Add(new()
            {
                group.Key, group.Count().ToString(),
                gIssued.ToString("C", Money), gCollected.ToString("C", Money), (gIssued - gCollected).ToString("C", Money)
            });
        }
        report.Sections.Add(byBranch);

        var detail = new ReportSection
        {
            Heading = "Fine ledger",
            Columns = new() { "Loan #", "Card", "Borrower", "Item code", "Returned at", "Due", "Returned", "Days late", "Fine", "Status", "Paid on" },
            EmptyMessage = "No fines were issued for this filter."
        };
        foreach (var loan in fined)
        {
            detail.Rows.Add(new()
            {
                loan.Id.ToString(),
                loan.Borrower?.CardNumber ?? "-",
                loan.Borrower?.FullName ?? "-",
                loan.Item?.LibraryCode ?? "-",
                BranchName(loan.ReturnBranchId, branches),
                loan.DueDate.ToString("yyyy-MM-dd"),
                loan.ReturnedDate?.ToString("yyyy-MM-dd") ?? "-",
                loan.DaysOverdue(loan.ReturnedDate ?? loan.DueDate).ToString(),
                (loan.FineAmount ?? 0).ToString("C", Money),
                loan.FinePaid ? "Paid" : "Unpaid",
                loan.FinePaidDate?.ToString("yyyy-MM-dd") ?? "-"
            });
        }
        report.Sections.Add(detail);

        return report;
    }

    // ------------------------------------------------------------------
    // 3. Inventory health
    // ------------------------------------------------------------------
    private async Task<Report> InventoryHealthAsync(ReportFilter filter, List<Branch> branches)
    {
        var allItems = await _items.GetAllAsync();
        var loans = await _loans.GetAllAsync();
        var activeReservations = await _reservations.GetAllActiveAsync();
        var openTransfers = (await _transfers.GetAllAsync()).Where(t => t.IsOpen).ToList();

        var items = allItems
            .Where(i => filter.BranchId is null || i.CurrentBranchId == filter.BranchId)
            .ToList();

        var borrowedItemIds = loans.Select(l => l.ItemId).ToHashSet();
        var waitlistCounts = activeReservations.GroupBy(r => r.ItemId).ToDictionary(g => g.Key, g => g.Count());
        var inTransitIds = openTransfers.Select(t => t.ItemId).ToHashSet();

        int usable = items.Count(i => i.Status is ItemStatus.Available or ItemStatus.Borrowed);

        var report = new Report { Title = "Inventory health" };
        report.Summary.Add(new("Items", items.Count.ToString()));
        report.Summary.Add(new("Usable (available or on loan)",
            items.Count == 0 ? "0" : $"{usable} ({100.0 * usable / items.Count:0}%)"));
        report.Summary.Add(new("Damaged / destroyed",
            $"{items.Count(i => i.Status == ItemStatus.Damaged)} / {items.Count(i => i.Status == ItemStatus.Destroyed)}"));
        report.Summary.Add(new("Never borrowed", items.Count(i => !borrowedItemIds.Contains(i.Id)).ToString()));
        report.Summary.Add(new("In transit between branches", items.Count(i => inTransitIds.Contains(i.Id)).ToString()));
        report.Summary.Add(new("Items with a waitlist", items.Count(i => waitlistCounts.ContainsKey(i.Id)).ToString()));

        var byBranch = new ReportSection
        {
            Heading = "Stock by current location",
            Columns = new() { "Branch", "Items", "Available", "On loan", "Damaged", "Destroyed", "Away from home", "Usable %" }
        };
        foreach (var group in items.GroupBy(i => BranchName(i.CurrentBranchId, branches)).OrderBy(g => g.Key))
        {
            int total = group.Count();
            int ok = group.Count(i => i.Status is ItemStatus.Available or ItemStatus.Borrowed);
            byBranch.Rows.Add(new()
            {
                group.Key,
                total.ToString(),
                group.Count(i => i.Status == ItemStatus.Available).ToString(),
                group.Count(i => i.Status == ItemStatus.Borrowed).ToString(),
                group.Count(i => i.Status == ItemStatus.Damaged).ToString(),
                group.Count(i => i.Status == ItemStatus.Destroyed).ToString(),
                group.Count(i => i.HomeBranchId.HasValue && i.CurrentBranchId != i.HomeBranchId).ToString(),
                total == 0 ? "-" : $"{100.0 * ok / total:0}%"
            });
        }
        report.Sections.Add(byBranch);

        var attention = new ReportSection
        {
            Heading = "Items needing attention",
            Columns = new() { "Item", "Code", "Type", "Status", "Location", "Waitlist", "Issue" },
            EmptyMessage = "Nothing needs attention for this filter."
        };
        foreach (var item in items.OrderBy(i => i.Name))
        {
            var issues = new List<string>();
            if (item.Status == ItemStatus.Damaged) issues.Add("Damaged - repair or replace");
            if (item.Status == ItemStatus.Destroyed) issues.Add("Destroyed - remove from shelf list");
            if (inTransitIds.Contains(item.Id)) issues.Add("In transit");
            if (waitlistCounts.TryGetValue(item.Id, out int waiting) && waiting >= 2) issues.Add("High demand");
            if (item.HomeBranchId.HasValue && item.CurrentBranchId != item.HomeBranchId) issues.Add("Away from home branch");

            if (issues.Count == 0) continue;

            attention.Rows.Add(new()
            {
                item.Name, item.LibraryCode, item.ItemType, item.Status.ToString(),
                BranchName(item.CurrentBranchId, branches),
                waitlistCounts.TryGetValue(item.Id, out int w) ? w.ToString() : "0",
                string.Join("; ", issues)
            });
        }
        report.Sections.Add(attention);

        return report;
    }

    private static string BranchName(int? branchId, List<Branch> branches) =>
        branches.FirstOrDefault(b => b.Id == branchId)?.Name ?? "Unassigned";

    private static string DescribeFilter(ReportFilter filter, List<Branch> branches)
    {
        string branch = filter.BranchId is null ? "All branches" : BranchName(filter.BranchId, branches);
        string from = filter.From?.ToString("d MMM yyyy", CultureInfo.InvariantCulture) ?? "the beginning";
        string to = filter.To?.ToString("d MMM yyyy", CultureInfo.InvariantCulture) ?? "today";
        return $"{branch} | {from} to {to}";
    }
}
