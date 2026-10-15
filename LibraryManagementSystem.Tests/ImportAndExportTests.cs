using System.Text;
using LibraryManagementSystem.Api;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Services;
using LibraryManagementSystem.Services.Reports;
using LibraryManagementSystem.Tests.TestSupport;
using Microsoft.Extensions.Options;

namespace LibraryManagementSystem.Tests;

/// <summary>CSV parsing/import validation, the mock provider, report exports and the API key check.</summary>
public class ImportAndExportTests
{
    private readonly InMemoryLibrary _db = new();
    private readonly Branch _darwin;

    public ImportAndExportTests()
    {
        _darwin = _db.AddBranch("DAR", "Darwin City");
        _db.AddBranch("CAS", "Casuarina");
    }

    // ---------------- CSV ----------------

    [Fact]
    public void Csv_parser_handles_quotes_commas_and_blank_lines()
    {
        var rows = CsvParser.Parse("a,b,c\r\n\"x, y\",\"he said \"\"hi\"\"\",z\n\n1,2,3");

        Assert.Equal(3, rows.Count);
        Assert.Equal("x, y", rows[1][0]);
        Assert.Equal("he said \"hi\"", rows[1][1]);
        Assert.Equal(new[] { "1", "2", "3" }, rows[2]);
    }

    [Fact]
    public async Task Import_preview_flags_each_kind_of_bad_row()
    {
        _db.AddBook("BK-0001", "Existing", _darwin);
        var csv = string.Join("\n",
            "ItemType,Name,LibraryCode,Author,Artist,Year,ToyType,RecommendedAge,BranchCode",
            "Book,Good Book,BK-9001,Someone,,,,,DAR",           // 2: OK
            "Book,No Author,BK-9002,,,,,,DAR",                  // 3: missing author
            "Video,Wrong Type,VD-0001,,,,,,DAR",                // 4: bad type
            "Book,Duplicate In DB,BK-0001,Someone,,,,,DAR",     // 5: code exists
            "Book,Dup In File,BK-9001,Someone,,,,,DAR",         // 6: duplicate within file
            "Music,Bad Year,MU-9001,,Band,1850,,,DAR",          // 7: year out of range
            "Toy,Bad Branch,TY-9001,,,,Puzzle,5,XXX",           // 8: unknown branch
            "music,Lower Case Type,MU-9002,,Band,1999,,,");     // 9: OK, default branch

        var preview = await _db.Importer.PreviewCsvAsync(csv, "CAS");

        Assert.Empty(preview.FileErrors);
        Assert.Equal(2, preview.ValidCount);
        Assert.Contains("Author", string.Join(" ", preview.Rows.Single(r => r.RowNumber == 3).Errors));
        Assert.Contains("ItemType", string.Join(" ", preview.Rows.Single(r => r.RowNumber == 4).Errors));
        Assert.Contains("already in the catalogue", string.Join(" ", preview.Rows.Single(r => r.RowNumber == 5).Errors));
        Assert.Contains("more than once", string.Join(" ", preview.Rows.Single(r => r.RowNumber == 6).Errors));
        Assert.Contains("Year", string.Join(" ", preview.Rows.Single(r => r.RowNumber == 7).Errors));
        Assert.Contains("BranchCode", string.Join(" ", preview.Rows.Single(r => r.RowNumber == 8).Errors));
        Assert.Equal("CAS", preview.Rows.Single(r => r.RowNumber == 9).BranchCode);
    }

    [Fact]
    public async Task Import_saves_only_valid_rows_with_branch_set()
    {
        var csv = "ItemType,Name,LibraryCode,Author,BranchCode\nBook,Good,BK-9001,A,DAR\nBook,Bad,BK-9002,,DAR";
        var preview = await _db.Importer.PreviewCsvAsync(csv, "DAR");

        int count = await _db.Importer.ImportValidRowsAsync(preview);

        Assert.Equal(1, count);
        var item = Assert.Single(_db.Items);
        Assert.Equal("BK-9001", item.LibraryCode);
        Assert.Equal(_darwin.Id, item.HomeBranchId);
        Assert.Equal(_darwin.Id, item.CurrentBranchId);
        Assert.Equal(ItemStatus.Available, item.Status);
    }

    [Fact]
    public async Task Missing_required_column_is_a_file_error()
    {
        var preview = await _db.Importer.PreviewCsvAsync("Name,Author\nX,Y", "DAR");

        Assert.Contains(preview.FileErrors, e => e.Contains("ItemType"));
        Assert.False(preview.CanImport);
    }

    [Fact]
    public async Task External_record_can_be_imported_once_per_library_code()
    {
        var results = await new InstantMetadataProvider().SearchAsync("tolkien");
        Assert.True(results.Count >= 2);

        var item = await _db.Importer.ImportExternalRecordAsync(results[0].ExternalId, "bk-5000", _darwin.Id);
        Assert.Equal("BK-5000", item.LibraryCode);
        Assert.IsType<Book>(item);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _db.Importer.ImportExternalRecordAsync(results[1].ExternalId, "BK-5000", _darwin.Id));
    }

    // ---------------- Exports ----------------

    private static Report SampleReport() => new()
    {
        Title = "Test report",
        Subtitle = "All branches",
        GeneratedAtUtc = new DateTime(2026, 10, 14),
        Summary = { new("Total", "3") },
        Sections =
        {
            new ReportSection
            {
                Heading = "Rows",
                Columns = { "Name", "Note" },
                Rows = { new() { "=HYPERLINK(\"evil\")", "has, comma" }, new() { "Plain (brackets)", "ok" } }
            }
        }
    };

    [Fact]
    public void Csv_export_escapes_commas_and_blocks_formula_injection()
    {
        var text = Encoding.UTF8.GetString(new CsvReportExporter().Export(SampleReport()));

        Assert.Contains("\"'=HYPERLINK(\"\"evil\"\")\"", text);
        Assert.Contains("\"has, comma\"", text);
        Assert.Equal("'+1", CsvReportExporter.Escape("+1"));
    }

    [Fact]
    public void Pdf_export_produces_a_well_formed_pdf_file()
    {
        var bytes = new PdfReportExporter().Export(SampleReport());
        var text = Encoding.Latin1.GetString(bytes);

        Assert.StartsWith("%PDF-1.4", text);
        Assert.Contains("/Type /Catalog", text);
        Assert.Contains("startxref", text);
        Assert.EndsWith("%%EOF\n", text);
        Assert.Contains("Plain \\(brackets\\)", text); // brackets escaped inside PDF strings
    }

    [Fact]
    public void Long_pdf_reports_continue_onto_more_pages()
    {
        var report = SampleReport();
        for (int i = 0; i < 200; i++)
        {
            report.Sections[0].Rows.Add(new() { $"Row {i}", "x" });
        }

        var text = Encoding.Latin1.GetString(new PdfReportExporter().Export(report));
        int pages = text.Split("/Type /Page ").Length - 1;
        Assert.True(pages >= 4, $"expected several pages, got {pages}");
    }

    // ---------------- API key ----------------

    [Fact]
    public void Api_key_validator_accepts_only_configured_keys()
    {
        var validator = new ApiKeyValidator(Options.Create(new ApiSettings
        {
            Clients = { new ApiClientKey { Name = "Council website", Key = "secret-key-1" } }
        }));

        Assert.Equal("Council website", validator.Validate("secret-key-1"));
        Assert.Null(validator.Validate("secret-key-2"));
        Assert.Null(validator.Validate(""));
        Assert.Null(validator.Validate(null));
    }
}
