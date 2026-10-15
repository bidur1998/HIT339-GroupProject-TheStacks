using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models.ViewModels;

/// <summary>
/// One form serves all three item types. The admin picks a Type, and only
/// the fields relevant to that type are shown (toggled client-side in
/// site.js) and validated server-side in ItemsController. This avoids
/// three near-identical Create/Edit view pairs while still producing a
/// proper Book, Toy or Music instance underneath.
/// </summary>
public class ItemFormViewModel
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Item Type")]
    public string ItemType { get; set; } = "Book";

    [Required(ErrorMessage = "Every item needs a name.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Every item needs a library code.")]
    [StringLength(20)]
    [Display(Name = "Library Code")]
    public string LibraryCode { get; set; } = string.Empty;

    public ItemStatus Status { get; set; } = ItemStatus.Available;

    // --- Group Project: multi-branch ---
    [Required(ErrorMessage = "Choose which branch owns this item.")]
    [Display(Name = "Home branch")]
    public int? HomeBranchId { get; set; }

    /// <summary>Read-only on the form - only returns and transfers move an item.</summary>
    public string? CurrentBranchName { get; set; }

    // --- Book fields ---
    [StringLength(150)]
    public string? Author { get; set; }
    [StringLength(80)]
    public string? Genre { get; set; }

    // --- Music fields ---
    [StringLength(150)]
    public string? Artist { get; set; }
    [Range(1900, 2100)]
    public int? Year { get; set; }

    // --- Toy fields ---
    [StringLength(80)]
    public string? ToyType { get; set; }
    [Range(0, 18)]
    [Display(Name = "Recommended Age")]
    public int? RecommendedAge { get; set; }

    public static ItemFormViewModel FromItem(Item item)
    {
        var vm = new ItemFormViewModel
        {
            Id = item.Id,
            ItemType = item.ItemType,
            Name = item.Name,
            Description = item.Description,
            LibraryCode = item.LibraryCode,
            Status = item.Status,
            HomeBranchId = item.HomeBranchId,
            CurrentBranchName = item.CurrentBranch?.Name
        };

        switch (item)
        {
            case Book b:
                vm.Author = b.Author;
                vm.Genre = b.Genre;
                break;
            case Music m:
                vm.Artist = m.Artist;
                vm.Year = m.Year;
                break;
            case Toy t:
                vm.ToyType = t.ToyType;
                vm.RecommendedAge = t.RecommendedAge;
                break;
        }

        return vm;
    }
}
