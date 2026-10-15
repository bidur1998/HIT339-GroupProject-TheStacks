using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly ApplicationDbContext _context;

    public NotificationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(NotificationLog log)
    {
        _context.NotificationLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    public async Task<List<NotificationLog>> SearchAsync(NotificationType? type, NotificationChannel? channel, string? recipientOrSubject, int take = 200)
    {
        var logs = _context.NotificationLogs.Include(n => n.Borrower).AsQueryable();

        if (type.HasValue)
        {
            logs = logs.Where(n => n.Type == type.Value);
        }

        if (channel.HasValue)
        {
            logs = logs.Where(n => n.Channel == channel.Value);
        }

        if (!string.IsNullOrWhiteSpace(recipientOrSubject))
        {
            var q = recipientOrSubject.Trim().ToLower();
            logs = logs.Where(n => n.Recipient.ToLower().Contains(q) || n.Subject.ToLower().Contains(q));
        }

        return await logs
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Take(take)
            .ToListAsync();
    }

    public async Task<bool> ExistsForLoanAsync(NotificationType type, int loanId, DateTime sinceUtc) =>
        await _context.NotificationLogs.AnyAsync(n => n.Type == type && n.LoanId == loanId && n.CreatedAt >= sinceUtc);

    public async Task<int> CountAsync() => await _context.NotificationLogs.CountAsync();
}
