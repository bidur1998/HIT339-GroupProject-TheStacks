using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models;

/// <summary>
/// A book. Adds the extra information required by the brief: author and
/// genre (requirement 1d).
/// </summary>
public class Book : Item
{
    [Required(ErrorMessage = "Please enter the author.")]
    [StringLength(150)]
    public string Author { get; set; } = string.Empty;

    [StringLength(80)]
    public string? Genre { get; set; }

    public override string ItemType => "Book";

    public override string ExtraInfoSummary =>
        string.IsNullOrWhiteSpace(Genre) ? $"by {Author}" : $"by {Author} ({Genre})";
}
