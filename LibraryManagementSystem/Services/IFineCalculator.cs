namespace LibraryManagementSystem.Services;

/// <summary>
/// Isolating fine calculation behind its own small interface means the
/// fine policy (currently: a flat rate per day overdue) can be swapped out
/// - a sliding scale, a cap, a different rate per item type - without
/// touching LoanService at all.
/// </summary>
public interface IFineCalculator
{
    decimal CalculateFine(DateTime dueDate, DateTime returnDate);
    int DaysOverdue(DateTime dueDate, DateTime returnDate);
}
