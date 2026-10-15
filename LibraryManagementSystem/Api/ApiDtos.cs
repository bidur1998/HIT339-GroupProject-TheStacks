namespace LibraryManagementSystem.Api;

// Data Transfer Objects for the public API. The API never returns EF entity
// classes directly: these records decide exactly which fields leave the
// system, so borrower details, loan history and internal ids of other
// tables can never leak out through the API by accident.

public record CatalogueItemDto(
    string LibraryCode,
    string Name,
    string Type,
    string Details,
    string? Description,
    string Status,
    bool IsAvailable,
    string? BranchCode,
    string? BranchName,
    int WaitlistLength);

public record PagedResult<T>(int Page, int PageSize, int TotalCount, int TotalPages, IReadOnlyList<T> Items);

public record CategoryDto(string Type, int ItemCount, int AvailableCount, IReadOnlyList<SubCategoryDto> SubCategories);

public record SubCategoryDto(string Name, int ItemCount);

public record BranchStatusDto(
    string Code,
    string Name,
    string? Address,
    string? Phone,
    string OpeningHours,
    bool IsOpenNow,
    string OperatingStatus,
    string? StatusNote,
    int ItemsOnShelf,
    int ItemsAvailable,
    DateTime CheckedAtLocalTime);

public record ServiceStatusDto(string Service, string Version, string Status, DateTime ServerTimeUtc, int BranchCount, int BranchesOpenNow);
