using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models;

/// <summary>
/// A toy. Adds the extra information required by the brief: type and
/// recommended age (requirement 1d).
/// </summary>
public class Toy : Item
{
    [Required(ErrorMessage = "Please enter the toy type.")]
    [StringLength(80)]
    public string ToyType { get; set; } = string.Empty;

    [Range(0, 18, ErrorMessage = "Please enter a recommended age between 0 and 18.")]
    [Display(Name = "Recommended Age")]
    public int? RecommendedAge { get; set; }

    public override string ItemType => "Toy";

    public override string ExtraInfoSummary =>
        RecommendedAge.HasValue ? $"{ToyType}, ages {RecommendedAge}+" : ToyType;
}
