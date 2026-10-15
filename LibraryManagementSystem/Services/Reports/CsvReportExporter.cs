using System.Text;

namespace LibraryManagementSystem.Services.Reports;

/// <summary>
/// Writes a report as CSV that opens cleanly in Excel. Every cell is
/// escaped, and cells that start with = + - @ are prefixed with an
/// apostrophe so a malicious item name or borrower name can't run as a
/// spreadsheet formula when the manager opens the file ("CSV injection").
/// </summary>
public class CsvReportExporter : IReportExporter
{
    public string Format => "csv";
    public string ContentType => "text/csv";
    public string FileExtension => "csv";

    public byte[] Export(Report report)
    {
        var sb = new StringBuilder();
        WriteRow(sb, new[] { report.Title });
        WriteRow(sb, new[] { report.Subtitle });
        WriteRow(sb, new[] { "Generated (UTC)", report.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm") });
        sb.AppendLine();

        foreach (var (label, value) in report.Summary)
        {
            WriteRow(sb, new[] { label, value });
        }

        foreach (var section in report.Sections)
        {
            sb.AppendLine();
            WriteRow(sb, new[] { section.Heading });
            WriteRow(sb, section.Columns);
            foreach (var row in section.Rows)
            {
                WriteRow(sb, row);
            }
        }

        // UTF-8 with BOM so Excel picks the right encoding.
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        return preamble.Concat(body).ToArray();
    }

    private static void WriteRow(StringBuilder sb, IEnumerable<string> cells)
    {
        sb.AppendLine(string.Join(",", cells.Select(Escape)));
    }

    public static string Escape(string? value)
    {
        value ??= string.Empty;

        // A lone "-" (used for "no value") is harmless, so it's left as is.
        if (value.Length > 0 && value != "-" && "=+-@\t\r".Contains(value[0]))
        {
            value = "'" + value;
        }

        bool needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
