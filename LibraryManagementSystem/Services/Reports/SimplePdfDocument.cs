using System.Globalization;
using System.Text;

namespace LibraryManagementSystem.Services.Reports;

/// <summary>
/// A deliberately small PDF writer: text only, standard PDF fonts
/// (Helvetica and Courier, which every PDF reader has built in), any number
/// of pages. It writes the PDF file structure by hand (objects, page tree,
/// content streams and the cross-reference table) instead of pulling in a
/// third-party PDF library, which keeps licensing simple for the project.
/// Limitation: plain ASCII text only - other characters are replaced.
/// </summary>
public class SimplePdfDocument
{
    public const string Helvetica = "F1";
    public const string HelveticaBold = "F2";
    public const string Courier = "F3";

    private readonly List<StringBuilder> _pages = new();

    public SimplePdfDocument(double pageWidth = 842, double pageHeight = 595)
    {
        // Default: A4 landscape, in points (1/72 inch) - wide tables fit better.
        PageWidth = pageWidth;
        PageHeight = pageHeight;
    }

    public double PageWidth { get; }
    public double PageHeight { get; }
    public int PageCount => _pages.Count;

    public void AddPage() => _pages.Add(new StringBuilder());

    public void DrawText(double x, double y, string font, double size, string text)
    {
        if (_pages.Count == 0)
        {
            AddPage();
        }

        var page = _pages[^1];
        page.Append("BT /").Append(font).Append(' ').Append(Num(size)).Append(" Tf ")
            .Append(Num(x)).Append(' ').Append(Num(y)).Append(" Td (")
            .Append(EscapeText(text)).Append(") Tj ET\n");
    }

    public void DrawLine(double x1, double y1, double x2, double y2, double width = 0.5)
    {
        if (_pages.Count == 0)
        {
            AddPage();
        }

        _pages[^1].Append(Num(width)).Append(" w ")
            .Append(Num(x1)).Append(' ').Append(Num(y1)).Append(" m ")
            .Append(Num(x2)).Append(' ').Append(Num(y2)).Append(" l S\n");
    }

    public byte[] ToBytes()
    {
        if (_pages.Count == 0)
        {
            AddPage();
        }

        // Object numbers: 1 catalog, 2 page tree, 3-5 fonts, then a
        // (page, content stream) pair for each page.
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            null!, // page tree - filled in below once page numbers are known
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>"
        };

        var pageObjectNumbers = new List<int>();
        foreach (var content in _pages)
        {
            int pageNumber = objects.Count + 1;
            int contentNumber = pageNumber + 1;
            pageObjectNumbers.Add(pageNumber);

            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Num(PageWidth)} {Num(PageHeight)}] " +
                        $"/Resources << /Font << /F1 3 0 R /F2 4 0 R /F3 5 0 R >> >> /Contents {contentNumber} 0 R >>");

            string stream = content.ToString();
            objects.Add($"<< /Length {Encoding.Latin1.GetByteCount(stream)} >>\nstream\n{stream}endstream");
        }

        objects[1] = $"<< /Type /Pages /Kids [{string.Join(" ", pageObjectNumbers.Select(n => $"{n} 0 R"))}] /Count {pageObjectNumbers.Count} >>";

        // Write the file, remembering each object's byte offset for the xref table.
        using var output = new MemoryStream();
        void Write(string s)
        {
            var bytes = Encoding.Latin1.GetBytes(s);
            output.Write(bytes, 0, bytes.Length);
        }

        Write("%PDF-1.4\n");
        var offsets = new List<long>();
        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(output.Position);
            Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        long xrefStart = output.Position;
        Write($"xref\n0 {objects.Count + 1}\n");
        Write("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            Write($"{offset:D10} 00000 n \n");
        }
        Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefStart}\n%%EOF\n");

        return output.ToArray();
    }

    /// <summary>Approximate width of text in points (exact for Courier, an estimate for Helvetica).</summary>
    public static double TextWidth(string text, string font, double size) =>
        text.Length * size * (font == Courier ? 0.6 : 0.52);

    private static string Num(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public static string EscapeText(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            char safe = c switch
            {
                '—' or '–' => '-',
                '‘' or '’' => '\'',
                '“' or '”' => '"',
                '…' => '.',
                _ => c
            };

            if (safe is '\\' or '(' or ')')
            {
                sb.Append('\\').Append(safe);
            }
            else if (safe < 32 || safe > 126)
            {
                sb.Append('?');
            }
            else
            {
                sb.Append(safe);
            }
        }
        return sb.ToString();
    }
}
