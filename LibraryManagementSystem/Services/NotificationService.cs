using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;

namespace LibraryManagementSystem.Services;

public class NotificationService : INotificationService
{
    private readonly IEnumerable<INotificationChannelSender> _senders;
    private readonly INotificationRepository _logs;
    private readonly TimeProvider _time;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IEnumerable<INotificationChannelSender> senders,
        INotificationRepository logs,
        TimeProvider time,
        ILogger<NotificationService> logger)
    {
        _senders = senders;
        _logs = logs;
        _time = time;
        _logger = logger;
    }

    public async Task<List<NotificationLog>> SendAsync(Borrower borrower, NotificationMessage message, int? loanId = null, int? reservationId = null)
    {
        var written = new List<NotificationLog>();

        foreach (var channel in ChannelsFor(borrower.NotificationPreference))
        {
            var sender = _senders.FirstOrDefault(s => s.Channel == channel);
            string recipient = channel == NotificationChannel.Email ? borrower.Email : borrower.PhoneNumber ?? string.Empty;
            string body = channel == NotificationChannel.Email ? message.EmailBody : message.SmsText;

            ChannelSendResult result;
            try
            {
                result = sender is null
                    ? new ChannelSendResult(false, $"No {channel} sender is configured.")
                    : await sender.SendAsync(recipient, message.Subject, body);
            }
            catch (Exception ex)
            {
                // A delivery problem must never break the library operation
                // that triggered it, so it is recorded instead of thrown.
                _logger.LogError(ex, "Notification send failed on {Channel}", channel);
                result = new ChannelSendResult(false, "Sender error: " + ex.Message);
            }

            var log = new NotificationLog
            {
                BorrowerId = borrower.Id,
                LoanId = loanId,
                ReservationId = reservationId,
                Type = message.Type,
                Channel = channel,
                Recipient = string.IsNullOrWhiteSpace(recipient) ? "(none on file)" : recipient,
                Subject = message.Subject,
                Body = body,
                CreatedAt = _time.GetUtcNow().UtcDateTime,
                DeliveryStatus = result.Delivered ? DeliveryStatus.Simulated : DeliveryStatus.Skipped,
                StatusDetail = result.Detail
            };

            await _logs.AddAsync(log);
            written.Add(log);
        }

        return written;
    }

    public static IEnumerable<NotificationChannel> ChannelsFor(NotificationPreference preference) => preference switch
    {
        NotificationPreference.Sms => new[] { NotificationChannel.Sms },
        NotificationPreference.EmailAndSms => new[] { NotificationChannel.Email, NotificationChannel.Sms },
        _ => new[] { NotificationChannel.Email }
    };
}

/// <summary>
/// SIMULATED email sender. Nothing is actually emailed - the message is
/// written to the application console log, and NotificationService saves it
/// to the NotificationLogs table for the dashboard.
/// </summary>
public class SimulatedEmailSender : INotificationChannelSender
{
    private readonly ILogger<SimulatedEmailSender> _logger;

    public SimulatedEmailSender(ILogger<SimulatedEmailSender> logger)
    {
        _logger = logger;
    }

    public NotificationChannel Channel => NotificationChannel.Email;

    public Task<ChannelSendResult> SendAsync(string recipient, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(recipient) || !recipient.Contains('@'))
        {
            return Task.FromResult(new ChannelSendResult(false, "No valid email address on file."));
        }

        _logger.LogInformation("[SIMULATED EMAIL] To: {Recipient} | Subject: {Subject}", recipient, subject);
        return Task.FromResult(new ChannelSendResult(true, "Simulated email (console + log table)."));
    }
}

/// <summary>SIMULATED SMS sender - same idea as the email simulator.</summary>
public class SimulatedSmsSender : INotificationChannelSender
{
    private readonly ILogger<SimulatedSmsSender> _logger;

    public SimulatedSmsSender(ILogger<SimulatedSmsSender> logger)
    {
        _logger = logger;
    }

    public NotificationChannel Channel => NotificationChannel.Sms;

    public Task<ChannelSendResult> SendAsync(string recipient, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return Task.FromResult(new ChannelSendResult(false, "No phone number on file."));
        }

        _logger.LogInformation("[SIMULATED SMS] To: {Recipient} | {Body}", recipient, body);
        return Task.FromResult(new ChannelSendResult(true, $"Simulated SMS ({body.Length} chars)."));
    }
}
