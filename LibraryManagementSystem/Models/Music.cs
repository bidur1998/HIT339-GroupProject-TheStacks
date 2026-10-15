using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models;

/// <summary>
/// A music item (album/CD/record). Adds the extra information required by
/// the brief: artist and year (requirement 1d).
/// </summary>
public class Music : Item
{
    [Required(ErrorMessage = "Please enter the artist.")]
    [StringLength(150)]
    public string Artist { get; set; } = string.Empty;

    [Range(1900, 2100, ErrorMessage = "Please enter a realistic year.")]
    public int? Year { get; set; }

    public override string ItemType => "Music";

    public override string ExtraInfoSummary =>
        Year.HasValue ? $"by {Artist} ({Year})" : $"by {Artist}";
}
