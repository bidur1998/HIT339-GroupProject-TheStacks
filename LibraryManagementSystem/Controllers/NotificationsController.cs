using LibraryManagementSystem.Models;
using LibraryManagementSystem.Models.ViewModels;
using LibraryManagementSystem.Repositories;
using LibraryManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagementSystem.Controllers;

/// <summary>
/// Notification log dashboard (Group Project - Automated Multi-Channel
/// Notification System). Every simulated email/SMS can be reviewed here.
/// Staff can also run the reminder check immediately instead of waiting
/// for the background timer, which makes the feature easy to test.
/// </summary>
[Authorize(Roles = Roles.Reception + "," + Roles.Manager + "," + Roles.Admin)]
public class NotificationsController : Controller
{
    private readonly INotificationRepository _logs;
    private readonly IReminderService _reminders;

    public NotificationsController(INotificationRepository logs, IReminderService reminders)
    {
        _logs = logs;
        _reminders = reminders;
    }

    public async Task<IActionResult> Index(NotificationType? type, NotificationChannel? channel, string? query)
    {
        var model = new NotificationsIndexViewModel
        {
            Type = type,
            Channel = channel,
            Query = query,
            Logs = await _logs.SearchAsync(type, channel, query),
            TotalCount = await _logs.CountAsync()
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Reception + "," + Roles.Admin)]
    public async Task<IActionResult> RunChecks()
    {
        var result = await _reminders.RunAsync();
        TempData["StatusMessage"] = "Reminder check finished: " + result;
        return RedirectToAction(nameof(Index));
    }
}
