using System.Text;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Models.ViewModels;
using LibraryManagementSystem.Repositories;
using LibraryManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LibraryManagementSystem.Controllers;

/// <summary>
/// External Book/Media Data Importer (Group Project). Admin-only.
/// CSV route: upload -> preview with per-row errors -> confirm (only valid
/// rows are saved, all in one batch). External route: search the mock
/// metadata provider and add a single record.
/// </summary>
[Authorize(Roles = Roles.Admin)]
public class ImportController : Controller
{
    private const string PreviewSessionKey = "Import_CsvText";
    private const long MaxFileBytes = 1_000_000; // 1 MB is plenty for 1000 rows

    private readonly ICatalogueImportService _importer;
    private readonly IMediaMetadataProvider _metadata;
    private readonly IBranchRepository _branches;
    private readonly BranchSettings _branchSettings;

    public ImportController(
        ICatalogueImportService importer,
        IMediaMetadataProvider metadata,
        IBranchRepository branches,
        IOptions<BranchSettings> branchSettings)
    {
        _importer = importer;
        _metadata = metadata;
        _branches = branches;
        _branchSettings = branchSettings.Value;
    }

    public async Task<IActionResult> Index(string? q)
    {
        var model = await BaseModelAsync();
        model.ExternalQuery = q;
        if (!string.IsNullOrWhiteSpace(q))
        {
            model.ExternalResults = await _metadata.SearchAsync(q, HttpContext.RequestAborted);
        }
        return View(model);
    }

    /// <summary>Downloads an example CSV with the expected header and one row per item type.</summary>
    public IActionResult Template()
    {
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", CatalogueImportService.AllColumns));
        csv.AppendLine("Book,The Very Hungry Caterpillar,A caterpillar eats its way through the week.,BK-1001,Eric Carle,Children's,,,,,DAR");
        csv.AppendLine("Music,Blue,,MU-1001,,,Joni Mitchell,1971,,,CAS");
        csv.AppendLine("Toy,Magnetic Tiles,\"32 pieces, mixed shapes\",TY-1001,,,,,Construction,3,PAL");
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "catalogue-import-template.csv");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2_000_000)]
    public async Task<IActionResult> Preview(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            TempData["ErrorMessage"] = "Please choose a CSV file to upload.";
            return RedirectToAction(nameof(Index));
        }

        if (!Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Only .csv files can be imported.";
            return RedirectToAction(nameof(Index));
        }

        if (file.Length > MaxFileBytes)
        {
            TempData["ErrorMessage"] = "That file is too large (limit 1 MB).";
            return RedirectToAction(nameof(Index));
        }

        string text;
        using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
        {
            text = await reader.ReadToEndAsync();
        }

        // Keep the uploaded text on the server between "preview" and
        // "confirm" so the admin doesn't have to upload it twice.
        HttpContext.Session.SetString(PreviewSessionKey, text);

        var model = await BaseModelAsync();
        model.Preview = await _importer.PreviewCsvAsync(text, _branchSettings.DefaultBranchCode);
        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm()
    {
        var text = HttpContext.Session.GetString(PreviewSessionKey);
        if (text is null)
        {
            TempData["ErrorMessage"] = "The upload has expired - please upload the file again.";
            return RedirectToAction(nameof(Index));
        }

        // Validate again rather than trusting the earlier preview: another
        // admin may have added one of these codes in the meantime.
        var preview = await _importer.PreviewCsvAsync(text, _branchSettings.DefaultBranchCode);
        if (!preview.CanImport)
        {
            TempData["ErrorMessage"] = "There are no valid rows to import.";
            return RedirectToAction(nameof(Index));
        }

        int imported = await _importer.ImportValidRowsAsync(preview);
        HttpContext.Session.Remove(PreviewSessionKey);

        TempData["StatusMessage"] = $"Imported {imported} item(s)." +
            (preview.InvalidCount > 0 ? $" {preview.InvalidCount} row(s) with errors were skipped." : string.Empty);
        return RedirectToAction("Index", "Items");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportExternal(string externalId, string libraryCode, int branchId, string? q)
    {
        try
        {
            var item = await _importer.ImportExternalRecordAsync(externalId, libraryCode, branchId);
            TempData["StatusMessage"] = $"'{item.Name}' was added to the catalogue as {item.LibraryCode}.";
        }
        catch (BusinessRuleException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { q });
    }

    private async Task<ImportIndexViewModel> BaseModelAsync() => new()
    {
        Branches = await _branches.GetAllAsync(),
        ProviderName = _metadata.ProviderName,
        DefaultBranchCode = _branchSettings.DefaultBranchCode
    };
}
