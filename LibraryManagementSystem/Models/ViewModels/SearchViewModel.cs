namespace LibraryManagementSystem.Models.ViewModels;

/// <summary>The public, phone-friendly search portal (requirement 4).</summary>
public class SearchViewModel
{
    public string? Query { get; set; }

    public string? TypeFilter { get; set; }

    public bool AvailableOnly { get; set; }

    // --- Group Project: multi-branch search ---
    public int? BranchId { get; set; }

    public List<Branch> Branches { get; set; } = new();

    public List<Item> Results { get; set; } = new();
}
