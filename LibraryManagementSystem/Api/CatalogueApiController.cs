using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;
using LibraryManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LibraryManagementSystem.Api;

/// <summary>
/// Public API &amp; Developer Endpoint (Group Project). A secure, read-only
/// REST API that external apps can use to query the catalogue, categories
/// and branch operating status.
///
/// Security measures:
/// - GET only (no POST/PUT/DELETE exist, so nothing can be changed);
/// - every request needs a valid X-Api-Key header ([RequireApiKey]);
/// - rate limited per key ("public-api" policy in Program.cs);
/// - returns DTOs, never EF entities, so no borrower data can leak;
/// - page size is capped so one request can't dump the whole database.
/// </summary>
[ApiController]
[Route("api/v1")]
[AllowAnonymous]                 // not a staff cookie login - the API key filter does the checking
[RequireApiKey]
[EnableRateLimiting("public-api")]
[EnableCors("PublicApi")]
[Produces("application/json")]
public class CatalogueApiController : ControllerBase
{
    private const int MaxPageSize = 50;

    private readonly IItemRepository _items;
    private readonly IBranchRepository _branches;
    private readonly IReservationRepository _reservations;
    private readonly IBranchContext _branchContext;
    private readonly TimeProvider _time;

    public CatalogueApiController(
        IItemRepository items,
        IBranchRepository branches,
        IReservationRepository reservations,
        IBranchContext branchContext,
        TimeProvider time)
    {
        _items = items;
        _branches = branches;
        _reservations = reservations;
        _branchContext = branchContext;
        _time = time;
    }

    /// <summary>GET /api/v1/status - is the service up, and how many branches are open.</summary>
    [HttpGet("status")]
    public async Task<ActionResult<ServiceStatusDto>> Status()
    {
        var branches = await _branches.GetAllAsync();
        var localNow = _branchContext.LocalNow;
        return new ServiceStatusDto(
            "The Stacks Library API", "v1", "OK",
            _time.GetUtcNow().UtcDateTime,
            branches.Count,
            branches.Count(b => b.IsOpenAt(localNow)));
    }

    /// <summary>
    /// GET /api/v1/catalogue?search=&amp;type=&amp;branch=&amp;availableOnly=&amp;page=&amp;pageSize=
    /// Searches the catalogue (same matching as the public search page).
    /// </summary>
    [HttpGet("catalogue")]
    public async Task<ActionResult<PagedResult<CatalogueItemDto>>> Catalogue(
        string? search, string? type, string? branch, bool availableOnly = false, int page = 1, int pageSize = 20)
    {
        if (type is not null && type is not ("Book" or "Toy" or "Music"))
        {
            return ValidationProblem(InvalidQuery("type must be Book, Toy or Music."));
        }

        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize)
        {
            return ValidationProblem(InvalidQuery($"page must be 1 or more and pageSize between 1 and {MaxPageSize}."));
        }

        int? branchId = null;
        if (!string.IsNullOrWhiteSpace(branch))
        {
            var found = await _branches.GetByCodeAsync(branch);
            if (found is null)
            {
                return NotFound(new ProblemDetails { Title = $"No branch with code '{branch}'.", Status = 404 });
            }
            branchId = found.Id;
        }

        var results = await _items.SearchAsync(search, type, availableOnly, branchId);
        var waitlists = await WaitlistCountsAsync();

        var pageItems = results
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => ToDto(i, waitlists))
            .ToList();

        int totalPages = (int)Math.Ceiling(results.Count / (double)pageSize);
        return new PagedResult<CatalogueItemDto>(page, pageSize, results.Count, totalPages, pageItems);
    }

    /// <summary>GET /api/v1/catalogue/{libraryCode} - one item.</summary>
    [HttpGet("catalogue/{libraryCode}")]
    public async Task<ActionResult<CatalogueItemDto>> CatalogueItem(string libraryCode)
    {
        var item = await _items.GetByLibraryCodeAsync(libraryCode);
        if (item is null)
        {
            return NotFound(new ProblemDetails { Title = $"No item with library code '{libraryCode}'.", Status = 404 });
        }

        return ToDto(item, await WaitlistCountsAsync());
    }

    /// <summary>GET /api/v1/categories - item types with counts, plus genres / toy types / artists' decades.</summary>
    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> Categories()
    {
        var items = await _items.GetAllAsync();

        CategoryDto Build(string type, IEnumerable<Item> ofType, Func<Item, string?> subCategory)
        {
            var list = ofType.ToList();
            var subs = list
                .GroupBy(i => subCategory(i) ?? "Uncategorised")
                .OrderBy(g => g.Key)
                .Select(g => new SubCategoryDto(g.Key, g.Count()))
                .ToList();
            return new CategoryDto(type, list.Count, list.Count(i => i.Status == ItemStatus.Available), subs);
        }

        return new List<CategoryDto>
        {
            Build("Book", items.OfType<Book>(), i => ((Book)i).Genre),
            Build("Toy", items.OfType<Toy>(), i => ((Toy)i).ToyType),
            Build("Music", items.OfType<Music>(), i => ((Music)i).Year is int y ? $"{y / 10 * 10}s" : null)
        };
    }

    /// <summary>GET /api/v1/branches - every branch and whether it is open right now.</summary>
    [HttpGet("branches")]
    public async Task<ActionResult<IEnumerable<BranchStatusDto>>> Branches()
    {
        var branches = await _branches.GetAllAsync();
        var items = await _items.GetAllAsync();
        return branches.Select(b => ToStatusDto(b, items)).ToList();
    }

    /// <summary>GET /api/v1/branches/{code}/status - operating status for one branch.</summary>
    [HttpGet("branches/{code}/status")]
    public async Task<ActionResult<BranchStatusDto>> BranchStatus(string code)
    {
        var branch = await _branches.GetByCodeAsync(code);
        if (branch is null)
        {
            return NotFound(new ProblemDetails { Title = $"No branch with code '{code}'.", Status = 404 });
        }

        return ToStatusDto(branch, await _items.GetAllAsync());
    }

    private BranchStatusDto ToStatusDto(Branch b, List<Item> items)
    {
        var localNow = _branchContext.LocalNow;
        var here = items.Where(i => i.CurrentBranchId == b.Id).ToList();
        return new BranchStatusDto(
            b.Code, b.Name, b.Address, b.PhoneNumber, b.HoursSummary,
            b.IsOpenAt(localNow),
            b.OperatingStatus == BranchOperatingStatus.Normal ? (b.IsOpenAt(localNow) ? "Open" : "Closed (outside opening hours)") : "Temporarily closed",
            b.StatusNote,
            here.Count(i => i.Status != ItemStatus.Borrowed),
            here.Count(i => i.Status == ItemStatus.Available),
            localNow);
    }

    private async Task<Dictionary<int, int>> WaitlistCountsAsync() =>
        (await _reservations.GetAllActiveAsync())
            .GroupBy(r => r.ItemId)
            .ToDictionary(g => g.Key, g => g.Count());

    private static CatalogueItemDto ToDto(Item i, Dictionary<int, int> waitlists) => new(
        i.LibraryCode,
        i.Name,
        i.ItemType,
        i.ExtraInfoSummary,
        i.Description,
        i.Status.ToString(),
        i.Status == ItemStatus.Available,
        i.CurrentBranch?.Code,
        i.CurrentBranch?.Name,
        waitlists.TryGetValue(i.Id, out int n) ? n : 0);

    private static ValidationProblemDetails InvalidQuery(string message)
    {
        var problem = new ValidationProblemDetails { Title = "Invalid query.", Status = 400 };
        problem.Errors.Add("query", new[] { message });
        return problem;
    }
}
