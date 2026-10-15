using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;
using Microsoft.Extensions.Options;

namespace LibraryManagementSystem.Services;

public class ReminderService : IReminderService
{
    private readonly ILoanRepository _loans;
    private readonly INotificationRepository _logs;
    private readonly INotificationService _notifications;
    private readonly IReservationService _reservations;
    private readonly IFineCalculator _fineCalculator;
    private readonly TimeProvider _time;
    private readonly NotificationSettings _settings;
    private readonly LibrarySettings _librarySettings;

    public ReminderService(
        ILoanRepository loans,
        INotificationRepository logs,
        INotificationService notifications,
        IReservationService reservations,
        IFineCalculator fineCalculator,
        TimeProvider time,
        IOptions<NotificationSettings> settings,
        IOptions<LibrarySettings> librarySettings)
    {
        _loans = loans;
        _logs = logs;
        _notifications = notifications;
        _reservations = reservations;
        _fineCalculator = fineCalculator;
        _time = time;
        _settings = settings.Value;
        _librarySettings = librarySettings.Value;
    }

    public async Task<ReminderRunResult> RunAsync()
    {
        var result = new ReminderRunResult();
        var now = _time.GetUtcNow().UtcDateTime;

        foreach (var loan in await _loans.GetActiveLoansAsync())
        {
            int daysUntilDue = (loan.DueDate.Date - now.Date).Days;

            if (daysUntilDue >= 0 && daysUntilDue <= _settings.DueSoonDays)
            {
                // One "due soon" reminder per loan.
                if (!await _logs.ExistsForLoanAsync(NotificationType.DueSoon, loan.Id, loan.BorrowedDate))
                {
                    await _notifications.SendAsync(loan.Borrower,
                        NotificationTemplates.DueSoon(loan, loan.Item.Name, daysUntilDue), loanId: loan.Id);
                    result.DueSoonSent++;
                }
            }
            else if (daysUntilDue < 0)
            {
                // At most one overdue/fine notice per loan per day, showing
                // the fine building up while the item is still out.
                if (!await _logs.ExistsForLoanAsync(NotificationType.OverdueFineAccruing, loan.Id, now.Date))
                {
                    int daysOverdue = _fineCalculator.DaysOverdue(loan.DueDate, now);
                    decimal fineSoFar = _fineCalculator.CalculateFine(loan.DueDate, now);
                    await _notifications.SendAsync(loan.Borrower,
                        NotificationTemplates.OverdueFineAccruing(loan, loan.Item.Name, daysOverdue, fineSoFar, _librarySettings.FinePerDayOverdue),
                        loanId: loan.Id);
                    result.OverdueSent++;
                }
            }
        }

        result.HoldsExpired = await _reservations.ExpireUncollectedHoldsAsync();
        return result;
    }
}

/// <summary>
/// Runs the reminder check on a timer for as long as the app is running -
/// this is what makes the notifications "automated". Each run gets its own
/// DI scope because DbContext/repositories are scoped services.
/// </summary>
public class ReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NotificationSettings _settings;
    private readonly ILogger<ReminderBackgroundService> _logger;

    public ReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<NotificationSettings> settings,
        ILogger<ReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.EnableBackgroundChecks)
        {
            _logger.LogInformation("Background reminder checks are turned off (NotificationSettings:EnableBackgroundChecks).");
            return;
        }

        // Give the app a moment to finish migrating/seeding on first start.
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _settings.CheckIntervalMinutes)));
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var reminders = scope.ServiceProvider.GetRequiredService<IReminderService>();
                var result = await reminders.RunAsync();
                _logger.LogInformation("Reminder check finished: {Result}", result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reminder check failed - will try again next interval.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
