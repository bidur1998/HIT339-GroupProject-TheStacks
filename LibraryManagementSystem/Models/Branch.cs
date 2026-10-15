using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models;

/// <summary>Whether a branch is running normally or has been closed by an admin (e.g. a power outage).</summary>
public enum BranchOperatingStatus
{
    Normal,
    TemporarilyClosed
}

/// <summary>
/// A physical library location (Group Project - Multi-Branch Inventory).
/// Items record which branch owns them (HomeBranch) and which branch is
/// physically holding them right now (CurrentBranch). Each installation of
/// the app is configured with a default branch code (BranchSettings in
/// appsettings.json), which is how one codebase runs at every location.
/// </summary>
public class Branch
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Every branch needs a short code.")]
    [StringLength(10)]
    [Display(Name = "Branch code")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Every branch needs a name.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Address { get; set; }

    [Phone]
    [StringLength(30)]
    [Display(Name = "Phone")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Opens at")]
    public TimeOnly OpeningTime { get; set; } = new(9, 0);

    [Display(Name = "Closes at")]
    public TimeOnly ClosingTime { get; set; } = new(17, 0);

    [Display(Name = "Open on weekends")]
    public bool OpenOnWeekends { get; set; }

    [Display(Name = "Operating status")]
    public BranchOperatingStatus OperatingStatus { get; set; } = BranchOperatingStatus.Normal;

    [StringLength(200)]
    [Display(Name = "Status note")]
    public string? StatusNote { get; set; }

    /// <summary>
    /// True when the branch is open at the given local time: not manually
    /// closed, open on that day of the week, and within opening hours.
    /// Used by the public API's "operating status" endpoint and the kiosk.
    /// </summary>
    public bool IsOpenAt(DateTime localTime)
    {
        if (OperatingStatus == BranchOperatingStatus.TemporarilyClosed)
        {
            return false;
        }

        bool isWeekend = localTime.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        if (isWeekend && !OpenOnWeekends)
        {
            return false;
        }

        var time = TimeOnly.FromDateTime(localTime);
        return time >= OpeningTime && time < ClosingTime;
    }

    public string HoursSummary =>
        $"{OpeningTime:HH\\:mm}-{ClosingTime:HH\\:mm} {(OpenOnWeekends ? "every day" : "Mon-Fri")}";
}
