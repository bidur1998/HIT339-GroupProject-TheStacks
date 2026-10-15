using Microsoft.Extensions.Options;

namespace LibraryManagementSystem.Services;

public class LibrarySettings
{
    public int DefaultLoanPeriodDays { get; set; } = 14;
    public decimal FinePerDayOverdue { get; set; } = 0.50m;
}

public class FineCalculator : IFineCalculator
{
    private readonly LibrarySettings _settings;

    public FineCalculator(IOptions<LibrarySettings> settings)
    {
        _settings = settings.Value;
    }

    public int DaysOverdue(DateTime dueDate, DateTime returnDate) =>
        Math.Max(0, (returnDate.Date - dueDate.Date).Days);

    public decimal CalculateFine(DateTime dueDate, DateTime returnDate) =>
        DaysOverdue(dueDate, returnDate) * _settings.FinePerDayOverdue;
}
