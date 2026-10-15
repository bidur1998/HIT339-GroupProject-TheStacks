namespace LibraryManagementSystem.Services.Reports;

/// <summary>
/// Lays a Report out on A4 landscape pages using SimplePdfDocument: title,
/// summary figures, then each table in a monospaced font so the columns
/// line up. Long tables continue onto new pages with the header repeated.
/// </summary>
public class PdfReportExporter : IReportExporter
{
    private const double Margin = 40;
    private const double TableFontSize = 8;
    private const double LineHeight = 11;
    private const int MaxColumnChars = 34;

    public string Format => "pdf";
    public string ContentType => "application/pdf";
    public string FileExtension => "pdf";

    public byte[] Export(Report report)
    {
        var pdf = new SimplePdfDocument();
        double y = 0;
        int pageNumber = 0;

        void NewPage()
        {
            pdf.AddPage();
            pageNumber++;
            y = pdf.PageHeight - Margin;
            pdf.DrawText(Margin, Margin / 2, SimplePdfDocument.Helvetica, 7,
                $"The Stacks Library - {report.Title} - generated {report.GeneratedAtUtc:yyyy-MM-dd HH:mm} UTC - page {pageNumber}");
        }

        void EnsureSpace(double needed)
        {
            if (y - needed < Margin)
            {
                NewPage();
            }
        }

        NewPage();
        pdf.DrawText(Margin, y, SimplePdfDocument.HelveticaBold, 16, report.Title);
        y -= 20;
        pdf.DrawText(Margin, y, SimplePdfDocument.Helvetica, 9, report.Subtitle);
        y -= 18;

        foreach (var (label, value) in report.Summary)
        {
            EnsureSpace(LineHeight);
            pdf.DrawText(Margin, y, SimplePdfDocument.HelveticaBold, 9, label + ":");
            pdf.DrawText(Margin + 210, y, SimplePdfDocument.Helvetica, 9, value);
            y -= LineHeight + 1;
        }

        double usableChars = (pdf.PageWidth - 2 * Margin) / (TableFontSize * 0.6);

        foreach (var section in report.Sections)
        {
            y -= 10;
            EnsureSpace(LineHeight * 4);
            pdf.DrawText(Margin, y, SimplePdfDocument.HelveticaBold, 11, section.Heading);
            y -= LineHeight + 4;

            if (section.Rows.Count == 0)
            {
                pdf.DrawText(Margin, y, SimplePdfDocument.Helvetica, 9, section.EmptyMessage ?? "No data for this filter.");
                y -= LineHeight;
                continue;
            }

            var widths = ColumnWidths(section, (int)usableChars);

            void Header()
            {
                pdf.DrawText(Margin, y, SimplePdfDocument.Courier, TableFontSize, FormatRow(section.Columns, widths));
                y -= 3;
                pdf.DrawLine(Margin, y, pdf.PageWidth - Margin, y);
                y -= LineHeight - 2;
            }

            Header();
            foreach (var row in section.Rows)
            {
                if (y - LineHeight < Margin)
                {
                    NewPage();
                    pdf.DrawText(Margin, y, SimplePdfDocument.HelveticaBold, 9, section.Heading + " (continued)");
                    y -= LineHeight + 2;
                    Header();
                }

                pdf.DrawText(Margin, y, SimplePdfDocument.Courier, TableFontSize, FormatRow(row, widths));
                y -= LineHeight;
            }
        }

        return pdf.ToBytes();
    }

    /// <summary>Each column is as wide as its longest value (capped), shrunk evenly if the page is too narrow.</summary>
    public static int[] ColumnWidths(ReportSection section, int maxTotalChars)
    {
        int count = section.Columns.Count;
        var widths = new int[count];
        for (int c = 0; c < count; c++)
        {
            int longest = section.Columns[c].Length;
            foreach (var row in section.Rows)
            {
                if (c < row.Count)
                {
                    longest = Math.Max(longest, row[c]?.Length ?? 0);
                }
            }
            widths[c] = Math.Min(longest, MaxColumnChars);
        }

        int gaps = (count - 1) * 2;
        while (widths.Sum() + gaps > maxTotalChars && widths.Max() > 6)
        {
            int widest = Array.IndexOf(widths, widths.Max());
            widths[widest]--;
        }

        return widths;
    }

    public static string FormatRow(IReadOnlyList<string> cells, int[] widths)
    {
        var parts = new string[widths.Length];
        for (int c = 0; c < widths.Length; c++)
        {
            string cell = c < cells.Count ? cells[c] ?? string.Empty : string.Empty;
            if (cell.Length > widths[c])
            {
                cell = widths[c] > 3 ? cell[..(widths[c] - 3)] + "..." : cell[..widths[c]];
            }
            parts[c] = cell.PadRight(widths[c]);
        }
        return string.Join("  ", parts).TrimEnd();
    }
}
