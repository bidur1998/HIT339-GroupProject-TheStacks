using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;
using Microsoft.Extensions.Options;

namespace LibraryManagementSystem.Services;

/// <summary>
/// Answers "which branch is this screen being used at?" for controllers.
///
/// Each installation has a default branch (BranchSettings:DefaultBranchCode),
/// which is how the same code runs at every location. Staff can also switch
/// their desk branch for the current session (useful when one computer is
/// used to demonstrate transfers between branches). Only controllers use
/// this - the services take a branch id as a normal parameter, so they stay
/// testable and don't know about HTTP sessions.
/// </summary>
public interface IBranchContext
{
    string CurrentBranchCode { get; }
    Task<Branch?> GetCurrentBranchAsync();
    void SetDeskBranch(string branchCode);

    /// <summary>The current time in the library's time zone (for opening hours).</summary>
    DateTime LocalNow { get; }
}

public class BranchContext : IBranchContext
{
    public const string SessionKey = "Desk_BranchCode";

    private readonly IHttpContextAccessor _http;
    private readonly IBranchRepository _branches;
    private readonly TimeProvider _time;
    private readonly BranchSettings _settings;

    public BranchContext(IHttpContextAccessor http, IBranchRepository branches, TimeProvider time, IOptions<BranchSettings> settings)
    {
        _http = http;
        _branches = branches;
        _time = time;
        _settings = settings.Value;
    }

    public string CurrentBranchCode =>
        _http.HttpContext?.Session.GetString(SessionKey) ?? _settings.DefaultBranchCode;

    public async Task<Branch?> GetCurrentBranchAsync() =>
        await _branches.GetByCodeAsync(CurrentBranchCode)
        ?? await _branches.GetByCodeAsync(_settings.DefaultBranchCode);

    public void SetDeskBranch(string branchCode) =>
        _http.HttpContext?.Session.SetString(SessionKey, branchCode.Trim().ToUpperInvariant());

    public DateTime LocalNow => ToLocal(_time.GetUtcNow().UtcDateTime, _settings.TimeZoneId);

    public static DateTime ToLocal(DateTime utc, string timeZoneId)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
        }
        catch (TimeZoneNotFoundException)
        {
            // Darwin is UTC+9:30 with no daylight saving - a safe fallback.
            return utc.AddHours(9.5);
        }
    }
}
