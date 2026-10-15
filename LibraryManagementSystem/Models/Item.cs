using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models;

/// <summary>
/// The Item superclass. Book, Toy and Music all inherit from this class
/// (requirement 1a). Everything every catalogue item has in common - name,
/// description, a unique library code and a status - lives here; anything
/// specific to one kind of item lives on that subclass instead.
///
/// Mapped with EF Core's Table-Per-Hierarchy (TPH) strategy: all three
/// subclasses share a single "Items" table, distinguished by a discriminator
/// column, which keeps CRUD, search and status-reporting queries simple
/// while still giving each item type its own class and extra fields.
/// </summary>
public abstract class Item
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Every item needs a name.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// The unique code printed on the item's physical library label.
    /// Reception staff use this code to borrow and return items, and the
    /// public can search by it too.
    /// </summary>
    [Required(ErrorMessage = "Every item needs a library code.")]
    [StringLength(20)]
    [Display(Name = "Library Code")]
    public string LibraryCode { get; set; } = string.Empty;

    public ItemStatus Status { get; set; } = ItemStatus.Available;

    public DateTime DateAdded { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The branch that owns this item (Group Project - Multi-Branch
    /// Inventory). Nullable only so the migration can run against an
    /// Assignment 2 database; DbInitializer assigns every item a branch.
    /// </summary>
    [Display(Name = "Home branch")]
    public int? HomeBranchId { get; set; }
    public Branch? HomeBranch { get; set; }

    /// <summary>
    /// The branch physically holding the item right now. Changes when the
    /// item is returned at another branch or a transfer is received.
    /// </summary>
    [Display(Name = "Current location")]
    public int? CurrentBranchId { get; set; }
    public Branch? CurrentBranch { get; set; }

    /// <summary>Holds placed on this item (Digital Reservation &amp; Waitlist Queue).</summary>
    public List<Reservation> Reservations { get; set; } = new();

    /// <summary>
    /// Navigation property back to every loan this item has ever been part
    /// of. Used by the manager statistics screens and to stop an item being
    /// deleted while it is still on loan.
    /// </summary>
    public List<Loan> Loans { get; set; } = new();

    /// <summary>
    /// A short, human-readable label for the concrete type of this item
    /// (e.g. "Book", "Toy", "Music"). Used throughout the views instead of
    /// repeating GetType().Name checks everywhere.
    /// </summary>
    public abstract string ItemType { get; }

    /// <summary>
    /// A single line combining the type-specific extra information
    /// (author/genre, artist/year, type/age) so list and search views can
    /// display something meaningful without knowing which subclass they
    /// are looking at.
    /// </summary>
    public abstract string ExtraInfoSummary { get; }
}
