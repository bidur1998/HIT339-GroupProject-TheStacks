using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Repositories;

public interface INotificationRepository
{
    Task AddAsync(NotificationLog log);

    /// <summary>Most recent first, optionally filtered, capped at <paramref name="take"/> rows.</summary>
    Task<List<NotificationLog>> SearchAsync(NotificationType? type, NotificationChannel? channel, string? recipientOrSubject, int take = 200);

    /// <summary>Used to avoid sending the same reminder twice (e.g. one "due soon" per loan).</summary>
    Task<bool> ExistsForLoanAsync(NotificationType type, int loanId, DateTime sinceUtc);

    Task<int> CountAsync();
}
