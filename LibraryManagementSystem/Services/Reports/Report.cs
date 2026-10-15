namespace LibraryManagementSystem.Services.Reports;

public enum ReportType
{
    Borrowing,
    FineAudit,
    InventoryHealth
}

/// <summary>Filters the manager can apply to any report.</summary>
public class ReportFilter
{
    public int? BranchId { get; set; }

    /// <summary>Inclusive start date (local date, compared against UTC dates by day).</summary>
    public DateTime? From { get; set; }

    /// <summary>Inclusive end date.</summary>
    public DateTime? To { get; set; }

    public bool InRange(DateTime? date)
    {
        if (date is null) return false;
        if (From.HasValue && date.Value.Date < From.Value.Date) return false;
        if (To.HasValue && date.Value.Date > To.Value.Date) return false;
        return true;
    }
}

/// <summary>
/// A finished report as plain rows of text. The same Report object is shown
/// on screen and handed to the CSV and PDF exporters, so all three always
/// show exactly the same numbers.
/// </summary>
public class Report
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }

    /// <summary>Headline figures shown above the tables (label, value).</summary>
    public List<KeyValuePair<string, string>> Summary { get; set; } = new();

    public List<ReportSection> Sections { get; set; } = new();
}

public class ReportSection
{
    public string Heading { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
    public string? EmptyMessage { get; set; }
}

/// <summary>Turns a Report into a downloadable file.</summary>
public interface IReportExporter
{
    string Format { get; }
    string ContentType { get; }
    string FileExtension { get; }
    byte[] Export(Report report);
}
