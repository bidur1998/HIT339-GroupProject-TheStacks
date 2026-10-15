using System.Globalization;
using System.Text.RegularExpressions;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;

namespace LibraryManagementSystem.Services;

/// <summary>One CSV row after checking - shown to the admin before anything is saved.</summary>
public class ImportRow
{
    public int RowNumber { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string LibraryCode { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? Genre { get; set; }
    public string? Artist { get; set; }
    public int? Year { get; set; }
    public string? ToyType { get; set; }
    public int? RecommendedAge { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public int? BranchId { get; set; }

    public List<string> Errors { get; set; } = new();
    public bool IsValid => Errors.Count == 0;
}

public class ImportPreview
{
    public List<ImportRow> Rows { get; set; } = new();

    /// <summary>Problems with the file as a whole (e.g. missing header columns).</summary>
    public List<string> FileErrors { get; set; } = new();

    public int ValidCount => Rows.Count(r => r.IsValid);
    public int InvalidCount => Rows.Count(r => !r.IsValid);
    public bool CanImport => FileErrors.Count == 0 && ValidCount > 0;
}

/// <summary>
/// External Book/Media Data Importer (Group Project). Two routes in:
/// (1) bulk CSV upload - previewed and validated row by row first, then the
/// valid rows saved in one batch; (2) pick a record from the (mock) external
/// metadata provider and add it as a single item.
/// </summary>
public interface ICatalogueImportService
{
    Task<ImportPreview> PreviewCsvAsync(string csvText, string defaultBranchCode);
    Task<int> ImportValidRowsAsync(ImportPreview preview);
    Task<Item> ImportExternalRecordAsync(string externalId, string libraryCode, int branchId);
}

public class CatalogueImportService : ICatalogueImportService
{
    public const int MaxRows = 1000;

    public static readonly string[] RequiredColumns = { "ItemType", "Name", "LibraryCode" };

    public static readonly string[] AllColumns =
    {
        "ItemType", "Name", "Description", "LibraryCode", "Author", "Genre",
        "Artist", "Year", "ToyType", "RecommendedAge", "BranchCode"
    };

    private static readonly Regex LibraryCodeFormat = new("^[A-Za-z0-9-]{2,20}$");

    private readonly IItemRepository _items;
    private readonly IBranchRepository _branches;
    private readonly IMediaMetadataProvider _metadata;

    public CatalogueImportService(IItemRepository items, IBranchRepository branches, IMediaMetadataProvider metadata)
    {
        _items = items;
        _branches = branches;
        _metadata = metadata;
    }

    public async Task<ImportPreview> PreviewCsvAsync(string csvText, string defaultBranchCode)
    {
        var preview = new ImportPreview();
        var lines = CsvParser.Parse(csvText ?? string.Empty);

        if (lines.Count == 0)
        {
            preview.FileErrors.Add("The file is empty.");
            return preview;
        }

        // Map header names to column positions (case-insensitive, any order).
        var header = lines[0];
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < header.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(header[i]) && !columns.ContainsKey(header[i]))
            {
                columns[header[i].Trim()] = i;
            }
        }

        foreach (var required in RequiredColumns.Where(c => !columns.ContainsKey(c)))
        {
            preview.FileErrors.Add($"Missing required column '{required}'. Download the template to see the expected header.");
        }

        if (lines.Count - 1 > MaxRows)
        {
            preview.FileErrors.Add($"The file has {lines.Count - 1} rows - the limit is {MaxRows} per import.");
        }

        if (preview.FileErrors.Any())
        {
            return preview;
        }

        string? Get(string[] row, string column) =>
            columns.TryGetValue(column, out int index) && index < row.Length && !string.IsNullOrWhiteSpace(row[index])
                ? row[index].Trim()
                : null;

        var branches = await _branches.GetAllAsync();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 1; i < lines.Count; i++)
        {
            var raw = lines[i];
            var row = new ImportRow
            {
                RowNumber = i + 1, // +1 so it matches the line number in Excel
                ItemType = Get(raw, "ItemType") ?? string.Empty,
                Name = Get(raw, "Name") ?? string.Empty,
                Description = Get(raw, "Description"),
                LibraryCode = (Get(raw, "LibraryCode") ?? string.Empty).ToUpperInvariant(),
                Author = Get(raw, "Author"),
                Genre = Get(raw, "Genre"),
                Artist = Get(raw, "Artist"),
                ToyType = Get(raw, "ToyType"),
                BranchCode = (Get(raw, "BranchCode") ?? defaultBranchCode).ToUpperInvariant()
            };

            ValidateRow(row, raw, Get, branches, seenCodes);
            preview.Rows.Add(row);
        }

        // Check every code against the database in one query.
        var existing = await _items.GetExistingLibraryCodesAsync(preview.Rows.Select(r => r.LibraryCode));
        foreach (var row in preview.Rows.Where(r => existing.Contains(r.LibraryCode)))
        {
            row.Errors.Add($"Library code {row.LibraryCode} is already in the catalogue.");
        }

        return preview;
    }

    private static void ValidateRow(
        ImportRow row,
        string[] raw,
        Func<string[], string, string?> get,
        List<Branch> branches,
        HashSet<string> seenCodes)
    {
        // Normalise the type so "book" or "BOOK" also work.
        row.ItemType = row.ItemType.ToLowerInvariant() switch
        {
            "book" => "Book",
            "toy" => "Toy",
            "music" => "Music",
            _ => row.ItemType
        };

        if (row.ItemType is not ("Book" or "Toy" or "Music"))
        {
            row.Errors.Add($"ItemType must be Book, Toy or Music (got '{row.ItemType}').");
        }

        if (string.IsNullOrWhiteSpace(row.Name))
        {
            row.Errors.Add("Name is required.");
        }
        else if (row.Name.Length > 150)
        {
            row.Errors.Add("Name must be 150 characters or fewer.");
        }

        if (row.Description?.Length > 1000)
        {
            row.Errors.Add("Description must be 1000 characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(row.LibraryCode))
        {
            row.Errors.Add("LibraryCode is required.");
        }
        else if (!LibraryCodeFormat.IsMatch(row.LibraryCode))
        {
            row.Errors.Add("LibraryCode must be 2-20 letters, digits or dashes.");
        }
        else if (!seenCodes.Add(row.LibraryCode))
        {
            row.Errors.Add($"Library code {row.LibraryCode} appears more than once in this file.");
        }

        switch (row.ItemType)
        {
            case "Book" when string.IsNullOrWhiteSpace(row.Author):
                row.Errors.Add("Books need an Author.");
                break;
            case "Music" when string.IsNullOrWhiteSpace(row.Artist):
                row.Errors.Add("Music needs an Artist.");
                break;
            case "Toy" when string.IsNullOrWhiteSpace(row.ToyType):
                row.Errors.Add("Toys need a ToyType.");
                break;
        }

        string? yearText = get(raw, "Year");
        if (yearText is not null)
        {
            if (int.TryParse(yearText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int year) && year is >= 1900 and <= 2100)
            {
                row.Year = year;
            }
            else
            {
                row.Errors.Add("Year must be a whole number between 1900 and 2100.");
            }
        }

        string? ageText = get(raw, "RecommendedAge");
        if (ageText is not null)
        {
            if (int.TryParse(ageText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int age) && age is >= 0 and <= 18)
            {
                row.RecommendedAge = age;
            }
            else
            {
                row.Errors.Add("RecommendedAge must be a whole number between 0 and 18.");
            }
        }

        var branch = branches.FirstOrDefault(b => b.Code.Equals(row.BranchCode, StringComparison.OrdinalIgnoreCase));
        if (branch is null)
        {
            row.Errors.Add($"Unknown BranchCode '{row.BranchCode}'.");
        }
        else
        {
            row.BranchId = branch.Id;
        }
    }

    public async Task<int> ImportValidRowsAsync(ImportPreview preview)
    {
        var items = preview.Rows.Where(r => r.IsValid).Select(BuildItem).ToList();
        if (items.Count == 0)
        {
            return 0;
        }

        await _items.AddRangeAsync(items);
        return items.Count;
    }

    public static Item BuildItem(ImportRow row)
    {
        Item item = row.ItemType switch
        {
            "Book" => new Book { Author = row.Author ?? string.Empty, Genre = row.Genre },
            "Music" => new Music { Artist = row.Artist ?? string.Empty, Year = row.Year },
            "Toy" => new Toy { ToyType = row.ToyType ?? string.Empty, RecommendedAge = row.RecommendedAge },
            _ => throw new InvalidOperationException($"Unknown item type '{row.ItemType}'.")
        };

        item.Name = row.Name;
        item.Description = row.Description;
        item.LibraryCode = row.LibraryCode;
        item.Status = ItemStatus.Available;
        item.HomeBranchId = row.BranchId;
        item.CurrentBranchId = row.BranchId;
        return item;
    }

    public async Task<Item> ImportExternalRecordAsync(string externalId, string libraryCode, int branchId)
    {
        var record = await _metadata.GetByIdAsync(externalId)
            ?? throw new BusinessRuleException("That record is no longer available from the provider.");

        libraryCode = (libraryCode ?? string.Empty).Trim().ToUpperInvariant();
        if (!LibraryCodeFormat.IsMatch(libraryCode))
        {
            throw new BusinessRuleException("Library code must be 2-20 letters, digits or dashes.");
        }

        if (await _items.LibraryCodeExistsAsync(libraryCode))
        {
            throw new BusinessRuleException($"Library code {libraryCode} is already in the catalogue.");
        }

        var branch = await _branches.GetByIdAsync(branchId)
            ?? throw new BusinessRuleException("Please choose a valid branch.");

        Item item = record.MediaType == "Music"
            ? new Music { Artist = record.Creator, Year = record.Year is >= 1900 and <= 2100 ? record.Year : null }
            : new Book { Author = record.Creator, Genre = record.Genre };

        item.Name = record.Title.Length > 150 ? record.Title[..150] : record.Title;
        item.Description = record.Description;
        item.LibraryCode = libraryCode;
        item.Status = ItemStatus.Available;
        item.HomeBranchId = branch.Id;
        item.CurrentBranchId = branch.Id;

        await _items.AddAsync(item);
        return item;
    }
}
