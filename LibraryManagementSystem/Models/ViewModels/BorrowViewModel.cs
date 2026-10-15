using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models.ViewModels;

/// <summary>Reception's "borrow an item" form (requirement 2a).</summary>
public class BorrowViewModel
{
    [Required(ErrorMessage = "Enter the item's library code.")]
    [Display(Name = "Library Code")]
    public string LibraryCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Select the borrower.")]
    [Display(Name = "Borrower")]
    public int BorrowerId { get; set; }

    [Range(1, 90)]
    [Display(Name = "Loan Period (days)")]
    public int LoanPeriodDays { get; set; } = 14;
}

/// <summary>Reception's "return an item" form (requirement 2b).</summary>
public class ReturnViewModel
{
    [Required(ErrorMessage = "Enter the item's library code.")]
    [Display(Name = "Library Code")]
    public string LibraryCode { get; set; } = string.Empty;

    [Display(Name = "Condition on return")]
    public ItemStatus ConditionOnReturn { get; set; } = ItemStatus.Available;
}

/// <summary>The confirmation shown after a successful return, including any fine.</summary>
public class ReturnResultViewModel
{
    public string ItemName { get; set; } = string.Empty;
    public string BorrowerName { get; set; } = string.Empty;
    public int DaysOverdue { get; set; }
    public decimal FineAmount { get; set; }
}
